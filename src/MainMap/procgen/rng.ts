export class RNG {
    private seed: number;

    constructor(seed: number) {
        this.seed = seed;
    }

    // Returns float between 0 and 1
    next(): number {
        this.seed = (this.seed * 9301 + 49297) % 233280;
        return this.seed / 233280;
    }

    // Returns float between min (inclusive) and max (exclusive)
    range(min: number, max: number): number {
        return min + this.next() * (max - min);
    }
    
    // Returns int between min (inclusive) and max (inclusive)
    intRange(min: number, max: number): number {
        return Math.floor(min + this.next() * (max - min + 1));
    }

    // Returns true with given probability [0...1]
    chance(prob: number): boolean {
        return this.next() < prob;
    }
}
