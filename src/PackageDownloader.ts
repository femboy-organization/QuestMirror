import OAL from "./OAL";
import Package from "./Package";

// Downloads the runtime tools required for device mirroring.
export default class PackageDownloader {
    private static instance: PackageDownloader;
    private requirePackages: Package[];

    private constructor() {
        this.requirePackages = [];
    }

    public static getInstance(): PackageDownloader {
        if (!this.instance) {
            this.instance = new PackageDownloader();
        }
        return this.instance;
    }

    public getRequirePackages(): Package[] {
        return this.requirePackages;
    }

    public async getHasDownloaded(): Promise<boolean> {
        const packages = await OAL.getInstance().checkPackages();
        this.requirePackages = packages;
        return packages.length === 0;
    }

    public downloadPackages(callback: (error?: Error) => void): void {
        OAL.getInstance().downloadPackages(this.requirePackages, callback);
    }
}