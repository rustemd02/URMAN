import { DIALOGUE_LINES } from '../data/dialogue_data';
import { Game } from '../game/Game';
import { DialogueLine } from '../types/game.types';

export class DialogueSystem {
    constructor(private readonly game: Game) {}

    public getAvailableLines(npcId: string): DialogueLine[] {
        return DIALOGUE_LINES
            .filter((line) => line.npcId === npcId)
            .filter((line) => line.requiredKeys.every((keyId) => this.game.state.hasKnowledgeKey(keyId)));
    }

    public getBestLine(npcId: string, preferredKeyId?: string): DialogueLine | undefined {
        const available = this.getAvailableLines(npcId);
        if (preferredKeyId) {
            const preferred = available.find((line) => line.requiredKeys.includes(preferredKeyId));
            if (preferred) return preferred;
        }
        return available.sort((a, b) => b.requiredKeys.length - a.requiredKeys.length)[0];
    }

    public useLine(lineId: string): DialogueLine | undefined {
        const line = DIALOGUE_LINES.find((candidate) => candidate.id === lineId);
        if (!line) return undefined;
        if (!line.requiredKeys.every((keyId) => this.game.state.hasKnowledgeKey(keyId))) return undefined;

        if (line.pressureDelta) {
            this.game.state.raisePressure(line.pressureDelta, `dialogue_${line.id}`);
        }
        if (line.unlocks?.length) {
            this.game.state.addKnowledgeKeys(line.unlocks, line.id);
        }
        if (line.flags?.length) {
            this.game.state.updateNpcState(line.npcId, {
                reactionLevel: Math.max(this.game.state.npcStates[line.npcId]?.reactionLevel ?? 0, line.requiredKeys.length),
                flags: line.flags,
            });
            for (const flag of line.flags) {
                if (flag.includes('pressure') || flag.includes('alerted') || flag.includes('forest')) {
                    this.game.state.addPressureFlag(flag);
                }
            }
        }
        this.game.state.completeBeat(`dialogue_${line.id}_used`);
        return line;
    }
}
