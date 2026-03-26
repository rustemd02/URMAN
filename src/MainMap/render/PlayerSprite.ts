/**
 * PlayerSprite - Responsibility: Procedural rendering of the funny stylized character.
 * Character: A tiny guy in a green tubeıtäyqa (Tatar skullcap).
 * Features: Visible hands, legs, walking animation phase.
 */
export class PlayerSprite {
    /**
     * Draws the stylized player to a CanvasRenderingContext2D.
     * @param ctx - The target canvas context.
     * @param sx - Screen X position (integer-rounded recommended).
     * @param sy - Screen Y position (integer-rounded recommended).
     * @param walkPhase - Current animation time (0 to 1 range loop).
     * @param facingRight - Boolean to mirror the character silhouette.
     */
    public draw(
        ctx: CanvasRenderingContext2D,
        sx: number,
        sy: number,
        walkPhase: number = 0,
        facingRight: boolean = true
    ): void {
        const x = Math.round(sx);
        const y = Math.round(sy);
        
        // Calculate animation offsets (sine-based leg/arm swing)
        const walkSwing = Math.sin(walkPhase * Math.PI * 2) * 5;
        const bounce = Math.abs(Math.cos(walkPhase * Math.PI * 2)) * 2;
        
        ctx.save();
        ctx.translate(x, y - bounce); // Slight bounce when walking
        
        // Face mirroring
        if (!facingRight) {
            ctx.scale(-1, 1);
        }

        // 1. SHADOW (Base)
        ctx.fillStyle = 'rgba(0,0,0,0.15)';
        ctx.beginPath();
        ctx.ellipse(0, 3, 10, 4, 0, 0, Math.PI * 2);
        ctx.fill();

        // 2. LEGS (Moving)
        ctx.fillStyle = '#1b2d4a'; // dark pants
        ctx.fillRect(-6, -15, 5, 16 + walkSwing);  // Left
        ctx.fillRect(1, -15, 5, 16 - walkSwing);   // Right
        
        // BOOTS
        ctx.fillStyle = '#3a2a1a';
        ctx.fillRect(-7, 1 + walkSwing, 7, 4);
        ctx.fillRect(0, 1 - walkSwing, 7, 4);

        // 3. BODY (Shirt)
        ctx.fillStyle = '#2e8b57'; // emerald/green shirt
        ctx.fillRect(-8, -35, 16, 22);

        // 4. HANDS (Moving)
        ctx.fillStyle = '#f5cba7'; // skin
        const armSwing = walkPhase > 0 ? -walkSwing * 0.8 : 0;
        // Left
        ctx.beginPath();
        ctx.ellipse(-11, -22 + armSwing, 3.5, 3.5, 0, 0, Math.PI * 2);
        ctx.fill();
        // Right
        ctx.beginPath();
        ctx.ellipse(11, -22 - armSwing, 3.5, 3.5, 0, 0, Math.PI * 2);
        ctx.fill();

        // 5. HEAD
        ctx.fillStyle = '#f5cba7';
        ctx.beginPath();
        ctx.arc(0, -45, 9, 0, Math.PI * 2);
        ctx.fill();

        // FACE (Minimalist)
        ctx.fillStyle = '#1a1a1a';
        ctx.beginPath();
        ctx.arc(4, -46, 1.5, 0, Math.PI * 2); // Eye 1
        ctx.fill();
        ctx.beginPath();
        ctx.arc(-1, -46, 1.5, 0, Math.PI * 2); // Eye 2 (if side view)
        ctx.fill();

        // 6. TÜBEITÄYQA (Tatar Skullcap)
        // Main cap body
        ctx.fillStyle = '#1a6b3a'; // deep green
        ctx.beginPath();
        ctx.ellipse(0, -54, 10, 4, 0, Math.PI, 2 * Math.PI); // top half-ellipse
        ctx.fill();
        ctx.beginPath();
        ctx.arc(0, -56, 7.5, Math.PI, 2 * Math.PI); // curved top dome
        ctx.fill();

        // Gold/Yellow embroidery line
        ctx.strokeStyle = '#e6b34b';
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.moveTo(-8, -55);
        ctx.lineTo(8, -55);
        ctx.stroke();

        // Tassel on top
        ctx.fillStyle = '#e6b34b';
        ctx.beginPath();
        ctx.arc(0, -62, 2, 0, Math.PI * 2);
        ctx.fill();
        ctx.lineWidth = 0.8;
        ctx.beginPath();
        ctx.moveTo(0, -59);
        ctx.lineTo(0, -62);
        ctx.stroke();

        ctx.restore();
    }
}
