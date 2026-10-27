import * as child from "child_process";
import extract from "extract-zip";
import fs from "fs";
import https from "https";
import path from "path";
import { promisify } from "util";
import { app } from "electron";

import Package from "./Package";
import StringUtil from "./StringUtil";

const execFile = promisify(child.execFile);
const SCRCPY_VERSION = "5.0.1";
const SCRCPY_URL =
    "https://github.com/Genymobile/scrcpy/releases/download/v" +
    SCRCPY_VERSION +
    "/scrcpy-win64-v" +
    SCRCPY_VERSION +
    ".zip";

// OS abstraction layer used by the UI and device controller.
export default class OAL {
    private static instance: OAL;
    private mirrorHostHandle: string | null = null;
    private scrcpyProcess: child.ChildProcess | null = null;
    private embeddingProcess: child.ChildProcess | null = null;

    private constructor() {}

    public static getInstance(): OAL {
        if (!this.instance) {
            this.instance = new OAL();
        }
        return this.instance;
    }

    public setMirrorHostWindowHandle(handle: string | null): void {
        this.mirrorHostHandle = handle;
    }

    public isMac(): boolean {
        return process.platform === "darwin";
    }

    private getScrcpyDirectory(): string {
        return path.join(app.getPath("userData"), "scrcpy");
    }

    private getScrcpyExecutable(): string {
        return path.join(this.getScrcpyDirectory(), "scrcpy.exe");
    }

    private getAdbExecutable(): string {
        return path.join(this.getScrcpyDirectory(), "adb.exe");
    }

    public async checkPackages(): Promise<Package[]> {
        const packages: Package[] = [];
        if (this.isMac()) {
            const homebrew = await this.checkHomebrew();
            if (homebrew) packages.push(homebrew);

            const scrcpy = await this.checkScrcpy();
            if (scrcpy) packages.push(scrcpy);

            const adb = await this.checkAdb();
            if (adb) packages.push(adb);
        } else {
            const scrcpy = await this.checkScrcpy();
            if (scrcpy) packages.push(scrcpy);
        }
        return packages;
    }

    public downloadPackages(
        packages: Package[],
        callback: (error?: Error) => void
    ): void {
        this.installPackages(packages)
            .then(() => callback())
            .catch((error: unknown) => {
                const normalized =
                    error instanceof Error ? error : new Error(String(error));
                callback(normalized);
            });
    }

    private async installPackages(packages: Package[]): Promise<void> {
        if (this.isMac()) {
            for (const item of packages) {
                if (item.getName() === "adb") {
                    await execFile("brew", ["install", "--cask", "android-platform-tools"]);
                } else if (item.getName() === "scrcpy") {
                    await execFile("brew", ["install", "scrcpy"]);
                }
            }
            return;
        }

        if (!packages.some((item) => item.getName() === "scrcpy")) {
            return;
        }

        const userData = app.getPath("userData");
        const zipPath = path.join(userData, "scrcpy-win64.zip");
        const stagingPath = path.join(userData, "scrcpy-staging");
        const destinationPath = this.getScrcpyDirectory();

        await fs.promises.mkdir(userData, { recursive: true });
        await fs.promises.rm(stagingPath, { recursive: true, force: true });
        await fs.promises.rm(destinationPath, { recursive: true, force: true });

        await this.downloadFile(SCRCPY_URL, zipPath);
        await fs.promises.mkdir(stagingPath, { recursive: true });
        await extract(zipPath, { dir: stagingPath });

        const executablePath = this.findFile(stagingPath, "scrcpy.exe");
        const adbPath = this.findFile(stagingPath, "adb.exe");
        if (!executablePath || !adbPath) {
            throw new Error(
                "The scrcpy archive was downloaded, but scrcpy.exe or adb.exe was not found."
            );
        }

        const extractedDirectory = path.dirname(executablePath);
        if (path.dirname(adbPath) !== extractedDirectory) {
            throw new Error("The scrcpy archive contains an unexpected file layout.");
        }

        if (extractedDirectory === stagingPath) {
            await fs.promises.rename(stagingPath, destinationPath);
        } else {
            await fs.promises.rename(extractedDirectory, destinationPath);
            await fs.promises.rm(stagingPath, { recursive: true, force: true });
        }

        await fs.promises.rm(zipPath, { force: true });
        if (
            !fs.existsSync(path.join(destinationPath, "scrcpy.exe")) ||
            !fs.existsSync(path.join(destinationPath, "adb.exe"))
        ) {
            throw new Error("scrcpy installation did not complete successfully.");
        }
    }

    private findFile(root: string, fileName: string): string | null {
        let entries: fs.Dirent[];
        try {
            entries = fs.readdirSync(root, { withFileTypes: true });
        } catch {
            return null;
        }

        for (const entry of entries) {
            const entryPath = path.join(root, entry.name);
            if (entry.isFile() && entry.name.toLowerCase() === fileName.toLowerCase()) {
                return entryPath;
            }
            if (entry.isDirectory()) {
                const nested = this.findFile(entryPath, fileName);
                if (nested) return nested;
            }
        }
        return null;
    }

    private downloadFile(
        url: string,
        destination: string,
        redirectCount: number = 0
    ): Promise<void> {
        return new Promise<void>((resolve, reject) => {
            if (redirectCount > 8) {
                reject(new Error("Too many redirects while downloading scrcpy."));
                return;
            }

            const request = https.get(url, (response) => {
                const status = response.statusCode ?? 0;
                if (
                    status >= 300 &&
                    status < 400 &&
                    response.headers.location
                ) {
                    const nextUrl = new URL(response.headers.location, url).toString();
                    response.resume();
                    this.downloadFile(nextUrl, destination, redirectCount + 1).then(
                        resolve,
                        reject
                    );
                    return;
                }

                if (status !== 200) {
                    response.resume();
                    reject(
                        new Error(
                            "Could not download scrcpy (HTTP " + status + ")."
                        )
                    );
                    return;
                }

                const output = fs.createWriteStream(destination);
                response.on("error", reject);
                output.on("error", reject);
                output.on("finish", () => {
                    output.close((error) => (error ? reject(error) : resolve()));
                });
                response.pipe(output);
            });

            request.setTimeout(60000, () => {
                request.destroy(new Error("Timed out while downloading scrcpy."));
            });
            request.on("error", reject);
        });
    }

    public async isDeviceConnected(): Promise<string | null> {
        try {
            const executable = this.isMac() ? "adb" : this.getAdbExecutable();
            const result = await execFile(executable, ["devices", "-l"]);
            return this.getDeviceSerial(result.stdout);
        } catch (error) {
            console.error("Unable to query Android devices:", error);
            return null;
        }
    }

    public launchSrccpy(args: string[], closeCallback: Function): void {
        const executable = this.isMac() ? "scrcpy" : this.getScrcpyExecutable();
        let finished = false;

        try {
            // In embedded Windows mode, fullscreen applies to the host window
            // (handled through IPC), not to the separate scrcpy top-level window.
            const processArgs =
                !this.isMac() && this.mirrorHostHandle
                    ? args.filter((argument) => argument !== "-f")
                    : args;
            const process = child.spawn(executable, processArgs, {
                windowsHide: !this.isMac()
            });
            this.scrcpyProcess = process;

            const finish = () => {
                if (finished) return;
                finished = true;
                if (this.scrcpyProcess === process) this.scrcpyProcess = null;
                this.stopEmbeddingProcess();
                closeCallback();
            };

            process.once("error", (error) => {
                console.error("Unable to start scrcpy:", error);
                finish();
            });
            process.once("close", finish);

            if (!this.isMac() && this.mirrorHostHandle && process.pid) {
                this.attachScrcpyWindow(process.pid);
            }
        } catch (error) {
            console.error("Unable to launch scrcpy:", error);
            this.stopEmbeddingProcess();
            closeCallback();
        }
    }

    private attachScrcpyWindow(scrcpyPid: number): void {
        if (!this.mirrorHostHandle) return;

        const scriptPath = path.join(
            __dirname,
            "..",
            "scripts",
            "embed-scrcpy-window.ps1"
        );
        if (!fs.existsSync(scriptPath)) {
            console.warn("The Windows window-embedding helper was not packaged.");
            return;
        }

        const process = child.spawn(
            "powershell.exe",
            [
                "-NoLogo",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-WindowStyle",
                "Hidden",
                "-File",
                scriptPath,
                this.mirrorHostHandle,
                String(scrcpyPid),
                "390"
            ],
            { windowsHide: true, stdio: "ignore" }
        );
        this.embeddingProcess = process;
        process.on("error", (error) => {
            console.warn("Could not attach the scrcpy window to SPECTRA:", error);
        });
    }

    private stopEmbeddingProcess(): void {
        if (this.embeddingProcess) {
            try {
                this.embeddingProcess.kill();
            } catch {
                // The helper may already have exited.
            }
            this.embeddingProcess = null;
        }
    }

    public async exitSrccpy(): Promise<void> {
        this.stopEmbeddingProcess();
        const running = this.scrcpyProcess;
        if (!running || !running.pid) return;

        try {
            if (this.isMac()) {
                await execFile("kill", [String(running.pid)]);
            } else {
                await execFile("taskkill", [
                    "/pid",
                    String(running.pid),
                    "/t",
                    "/f"
                ]);
            }
        } catch {
            // The process may have already exited.
        }
    }

    public async getIP(deviceSerial: string): Promise<string | null> {
        try {
            const executable = this.isMac() ? "adb" : this.getAdbExecutable();
            const result = await execFile(executable, [
                "-s",
                deviceSerial,
                "shell",
                "dumpsys",
                "wifi"
            ]);
            const lines = StringUtil.getLines(result.stdout);
            for (const line of lines) {
                if (!/ip[_ ]?address|ipAddress/i.test(line)) continue;
                const match = line.match(/((?:\d{1,3}\.){3}\d{1,3})/);
                if (match && match[1] !== "0.0.0.0") return match[1];
            }
        } catch (error) {
            console.error("Unable to read the Quest Wi-Fi IP:", error);
        }
        return null;
    }

    public async connectTCPIP(
        deviceSerial: string,
        ip: string,
        successCallback: Function,
        failedCallback: Function
    ): Promise<void> {
        try {
            const executable = this.isMac() ? "adb" : this.getAdbExecutable();
            await execFile(executable, ["-s", deviceSerial, "tcpip", "5555"]);
            const result = await execFile(executable, [
                "connect",
                ip + ":5555"
            ]);
            const output = (result.stdout + "\n" + result.stderr).toLowerCase();
            if (output.includes("connected to") || output.includes("already connected")) {
                successCallback();
            } else {
                failedCallback();
            }
        } catch (error) {
            console.error("Wireless ADB connection failed:", error);
            failedCallback();
        }
    }

    public discconct(): void {
        const executable = this.isMac() ? "adb" : this.getAdbExecutable();
        void execFile(executable, ["disconnect"]).catch(() => undefined);
    }

    private async checkHomebrew(): Promise<Package | null> {
        try {
            await execFile("brew", ["-v"]);
            return null;
        } catch {
            return new Package("homebrew", "https://brew.sh/");
        }
    }

    private async checkScrcpy(): Promise<Package | null> {
        if (this.isMac()) {
            try {
                await execFile("scrcpy", ["--version"]);
                return null;
            } catch {
                return new Package("scrcpy", "https://github.com/Genymobile/scrcpy");
            }
        }

        try {
            const scrcpyStat = await fs.promises.stat(this.getScrcpyExecutable());
            const adbStat = await fs.promises.stat(this.getAdbExecutable());
            if (scrcpyStat.isFile() && adbStat.isFile()) return null;
        } catch {
            // Re-download missing or incomplete runtime files.
        }
        return new Package("scrcpy", "https://github.com/Genymobile/scrcpy");
    }

    private async checkAdb(): Promise<Package | null> {
        try {
            await execFile("adb", ["--version"]);
            return null;
        } catch {
            return new Package(
                "adb",
                "https://developer.android.com/tools/releases/platform-tools"
            );
        }
    }

    private getDeviceSerial(stdout: string): string | null {
        const lines = StringUtil.getLines(stdout);
        for (let i = 1; i < lines.length; i++) {
            if (
                lines[i].indexOf("Quest") > 0 &&
                lines[i].indexOf("192.168") < 0
            ) {
                const end = lines[i].indexOf(" ");
                if (end > 0) return lines[i].slice(0, end);
            }
        }
        return null;
    }
}