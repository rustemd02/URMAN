import { GLBToSprite } from './GLBToSprite';

/**
 * AssetLoader - Utility to handle loading of images/textures and GLB 3D-to-2D renders.
 */
export class AssetLoader {
    private static images: Map<string, HTMLImageElement> = new Map();
    private static loadingPromises: Map<string, Promise<HTMLImageElement>> = new Map();

    /**
     * Loads an image from a URL and caches it. Supports .glb for 3D->2D rendering.
     */
    public static load(id: string, url: string, scale?: number, yOffset?: number): Promise<HTMLImageElement> {
        if (this.images.has(id)) return Promise.resolve(this.images.get(id)!);
        if (this.loadingPromises.has(id)) return this.loadingPromises.get(id)!;

        // If it's a 3D model, invoke the offline GLB->Sprite renderer
        if (url.endsWith('.glb')) {
            const promise = GLBToSprite.loadIsometricSprite(url, scale, yOffset).then(img => {
                this.images.set(id, img);
                this.loadingPromises.delete(id);
                return img;
            }).catch(e => {
                console.error(`Failed to load GLB sprite at ${url}`, e);
                this.loadingPromises.delete(id);
                throw e;
            });
            this.loadingPromises.set(id, promise);
            return promise;
        }

        const promise = new Promise<HTMLImageElement>((resolve, reject) => {
            const img = new Image();
            img.src = url;
            img.onload = () => {
                this.images.set(id, img);
                this.loadingPromises.delete(id);
                resolve(img);
            };
            img.onerror = () => {
                this.loadingPromises.delete(id);
                reject(new Error(`Failed to load image at ${url}`));
            };
        });

        this.loadingPromises.set(id, promise);
        return promise;
    }

    /**
     * Get a loaded image by its ID.
     */
    public static get(id: string): HTMLImageElement | undefined {
        return this.images.get(id);
    }

    /**
     * Load multiple assets at once.
     */
    public static async loadAll(assets: { id: string, url: string, scale?: number, yOffset?: number }[]): Promise<void> {
        await Promise.all(assets.map(a => this.load(a.id, a.url, a.scale, a.yOffset)));
    }
}
