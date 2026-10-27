import {app, BrowserWindow, dialog, ipcMain, shell} from 'electron'
import path from 'path'
import ExecController from './ExecController'
import OAL from './OAL'
import PackageDownloader from './PackageDownloader'
const fixPath = require('fix-path');

fixPath();

// メインウィンドウ
let mainWindow: Electron.BrowserWindow | null = null

// アプリが起動するとき
app.on('ready', async () => {
    
    // メインウィンドウを生成
    mainWindow = new BrowserWindow({
        title: "SPECTRA",
        width: 1400,
        height: 850,
        minWidth: 1050,
        minHeight: 640,
        backgroundColor: "#10141c",
        webPreferences: {
            nodeIntegration: true,
            // The existing UI uses Electron's IPC API from local bundled scripts.
            contextIsolation: false,
        },
    });

    mainWindow.removeMenu();

    const nativeHandle = mainWindow.getNativeWindowHandle();
    const nativeHandleValue =
        nativeHandle.length >= 8
            ? nativeHandle.readBigUInt64LE(0).toString()
            : nativeHandle.readUInt32LE(0).toString();
    OAL.getInstance().setMirrorHostWindowHandle(nativeHandleValue);

    // Stop child processes when the main application closes.
    mainWindow.on("closed", () => {
        ExecController.getInstance().stopMirroring();
        ExecController.getInstance().disconnect();
        OAL.getInstance().setMirrorHostWindowHandle(null);
        mainWindow = null;
    });

    mainWindow.loadFile(path.join(__dirname, "index.html"));

    // デバイスが接続されてないとき
    ipcMain.on("device_disconected", async () => {
        if (mainWindow) {
            const options: Electron.MessageBoxOptions = {
                title: 'No device is connected',
                type: 'info',
                buttons: ['OK', 'Cancel'],
                message: 'No device is connected.',
                detail: "Please connect your Oculus Quest device via USB cable."
            };
            let result = await dialog.showMessageBox(mainWindow, options);
            if (result.response == 1) { // アプリを閉じるとき
                app.quit()
            } else {
                let execController = ExecController.getInstance();
                execController.isDeviceConnected(
                    () => { mainWindow?.webContents.send("device_conected") },
                    () => { mainWindow?.webContents.send("device_disconected") }
                );
            }
        }
    })

    // デバイスがワイアレス接続されたとき
    ipcMain.on("device_wireless_connected", () => {
        if (mainWindow) {
            const options: Electron.MessageBoxOptions = {
                title: 'Device is connected',
                type: 'info',
                buttons: ['OK'],
                message: 'Device is connected wirelessly!',
                detail: "Please unplug USB cable."
            };
            dialog.showMessageBox(mainWindow, options);
        }
    })

    // UIControllerの設定が完了したとき
    ipcMain.on("UIController_is_ready", async () => {
        let hasDownloaded = await downloader.getHasDownloaded()
        if (hasDownloaded) {
            let execController = ExecController.getInstance();
            execController.isDeviceConnected(
                () => { mainWindow?.webContents.send("device_conected") },
                () => { mainWindow?.webContents.send("device_disconected") }
            );
        }
    })

    ipcMain.on("directory_select_button_clicked", async () => {
        if (!mainWindow) return;
        const result = await dialog.showOpenDialog(mainWindow, {
            properties: ["openDirectory", "createDirectory"],
        });
        if (!result.canceled && result.filePaths.length > 0) {
            mainWindow.webContents.send("directory_selected", result.filePaths[0]);
        }
    });

    ipcMain.on("mirror_fullscreen_changed", (_event, enabled: boolean) => {
        mainWindow?.setFullScreen(Boolean(enabled));
    });

    // キャプチャディレクトリが選択されていないとき
    ipcMain.on("no_capture_directory_is_selected", () => {
        if (mainWindow) {
            const options: Electron.MessageBoxOptions = {
                title: 'No capture directory is selected',
                type: 'info',
                buttons: ['OK'],
                message: 'No Capture Directory is Selected.',
                detail: "Please select a Directory to Save Capture Video."
            };
            dialog.showMessageBox(mainWindow, options);
        }
    })

    // scrcpyをダウンロードする
    let downloader = PackageDownloader.getInstance();
    let hasDownloaded = await downloader.getHasDownloaded(); 
    if (!hasDownloaded) { // 必要なパッケージがダウンロードされていないとき
        let requirePackages = downloader.getRequirePackages();
        // homebrewがインストールされていないとき
        if (requirePackages[0].getName() == "homebrew") {
            let detail = 'Please install Homebrew before launching this app.'
            detail +=  "\n" + "\n" 
            detail += "Press OK button to open the Github page(https://github.com/r-asada-ab/QuestMirror)."
            detail +=  "\n" + "\n" 
            detail += "Check Install on Mac."
            const options: Electron.MessageBoxOptions = {
                title: 'Download',
                type: 'info',
                buttons: ['OK', 'Cancel'],
                message: 'This app requires Homebrew',
                detail: detail
            };
            let result = await dialog.showMessageBox(mainWindow, options);
    
            if (result.response == 0) { // アプリを閉じるとき
                shell.openExternal("https://github.com/r-asada-ab/QuestMirror");
                app.quit();
            } else {
                app.quit();
            }
        } else {
            let detail = ""
            for (let i = 0; i < requirePackages.length; i++) {
                if (OAL.getInstance().isMac()) {
                    detail += requirePackages[i].toString() + "\n"
                } else {
                    detail += requirePackages[i].toString() + "\r\n"
                }
            }
            detail += "\n" + "Press button to download it."

            const options: Electron.MessageBoxOptions = {
                title: 'Download',
                type: 'info',
                buttons: ['OK', 'Cancel'],
                message: 'This app requires the following packages',
                detail: detail
            };
            let result = await dialog.showMessageBox(mainWindow, options);

            if (result.response == 1) { // アプリを閉じるとき
                app.quit()
            } else {
                mainWindow?.webContents.send("download_start");
                downloader.downloadPackages((error?: Error) => {
                    if (error) {
                        const message = error.message || String(error);
                        mainWindow?.webContents.send("download_error", message);
                        dialog.showErrorBox("SPECTRA could not download scrcpy", message);
                        return;
                    }
                    setTimeout(() => {
                        mainWindow?.webContents.send("download_end");
                    }, 250);
                });
            }
        }
    }
})