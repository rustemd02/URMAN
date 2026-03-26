/**
 * IsoCamera - Responsibility: Manages the 2D view state (Translation & Scale).
 * Keeps track of world-to-screen panning and zooming.
 */
export class IsoCamera {
    public x: number = 0;
    public y: number = 0;
    public scale: number = 1.0;

    /**
     * Shifts the camera by a delta.
     */
    public pan(dx: number, dy: number): void {
        this.x += dx;
        this.y += dy;
    }

    /**
     * Updates zoom scale while maintaining a screen pivot point.
     * @param factor - The zoom multiplier (e.g. 1.1 or 0.9).
     * @param px - Pivot X (e.g. mouse clientX).
     * @param py - Pivot Y (e.g. mouse clientY).
     * @param min - Minimum allowed scale.
     * @param max - Maximum allowed scale.
     */
    public zoomAt(factor: number, px: number, py: number, min: number = 0.4, max: number = 4.0): void {
        const nextScale = Math.max(min, Math.min(max, this.scale * factor));
        
        // Pivot math: shift (x,y) so pivot stays constant on screen
        this.x = px - (px - this.x) * (nextScale / this.scale);
        this.y = py - (py - this.y) * (nextScale / this.scale);
        this.scale = nextScale;
    }

    /**
     * Clamps camera coordinates within world pixel boundaries.
     */
    public clamp(minX: number, maxX: number, minY: number, maxY: number): void {
        this.x = Math.max(minX, Math.min(this.x, maxX));
        this.y = Math.max(minY, Math.min(this.y, maxY));
    }

    /**
     * Projects local world screen coordinates to final canvas screen coordinates.
     */
    public apply(worldX: number, worldY: number): { cx: number; cy: number } {
        return {
            cx: worldX * this.scale + this.x,
            cy: worldY * this.scale + this.y
        };
    }

    /**
     * Unprojects canvas coordinates back to local world screen coordinates.
     */
    public unproject(cx: number, cy: number): { wx: number; wy: number } {
        return {
            wx: (cx - this.x) / this.scale,
            wy: (cy - this.y) / this.scale
        };
    }
}
