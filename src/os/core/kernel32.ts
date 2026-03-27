/**
 * KERNEL32.DLL EXTENSION v1.0.4
 */
export class SystemTimer {
    private readonly END_DATE = new Date(2104, 0, 1).getTime();

    public render(container: HTMLElement) {
        if (!container) return;

        const updateCounter = () => {
            const now = new Date().getTime();
            const diff = this.END_DATE - now;
            const daysRemaining = Math.max(0, Math.floor(diff / (1000 * 60 * 60 * 24)));

            container.innerHTML = `
                <div style="
                    background: #c0c0c0;
                    border: 2px solid;
                    border-color: #dfdfdf #0a0a0a #0a0a0a #dfdfdf;
                    width: 200px;
                    padding: 1px;
                    font-family: 'MS Sans Serif', Arial, sans-serif;
                    color: #000;
                    box-shadow: 1px 1px 0 #000;
                ">
                    <div style="
                        background: #000080;
                        color: white;
                        padding: 2px 3px;
                        font-size: 11px;
                        font-weight: bold;
                        display: flex;
                        justify-content: space-between;
                        align-items: center;
                    ">
                        <div style="display: flex; align-items: center; gap: 3px;">
                            <div style="width: 12px; height: 12px; background: #c0c0c0; border: 1px solid #888;"></div>
                            <span>Timer</span>
                        </div>
                        <div style="
                            width: 14px; height: 12px; 
                            background: #c0c0c0; border: 1px solid; 
                            border-color: #fff #808080 #808080 #fff; 
                            color: black; font-size: 9px; 
                            text-align: center; line-height: 10px; 
                            cursor: pointer; font-family: sans-serif;
                        ">x</div>
                    </div>

                    <div style="padding: 10px;">
                        <div style="
                            background: #fff;
                            border: 2px inset #808080;
                            padding: 12px 5px;
                            text-align: center;
                            font-family: 'Courier New', monospace;
                        ">
                            <div style="font-size: 18px; font-weight: bold; color: #000;">
                                ${daysRemaining.toLocaleString().replace(/,/g, ' ')}
                            </div>
                            <div style="font-size: 10px; margin-top: 4px; color: #444; letter-spacing: 1px;">
                                КӨН КАЛДЫ
                            </div>
                        </div>
                        <div style="font-size: 9px; margin-top: 6px; color: #808080; text-align: right;">
                            V. 2104.0
                        </div>
                    </div>
                </div>
            `;
        };

        updateCounter();
        setInterval(updateCounter, 3600000);
    }
}