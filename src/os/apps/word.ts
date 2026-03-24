export const renderWord = (content: string = "") => `
  <div class="word-app" style="height:100%; background:#808080; padding:20px; display:flex; flex-direction:column; gap:10px;">
    <div class="word-toolbar" style="background:#c0c0c0; border:1px outset #fff; padding:2px; display:flex; gap:5px;">
        <button class="win98-btn" style="font-size:9px;">Файл</button>
        <button class="win98-btn" style="font-size:9px;">Правка</button>
        <button class="win98-btn" style="font-size:9px;">Вид</button>
    </div>
    <div class="word-page" style="flex:1; background:#fff; border:1px inset #000; box-shadow:5px 5px 0 rgba(0,0,0,0.2); padding:40px; overflow-y:auto; font-family:'Times New Roman', serif; font-size:14px; line-height:1.5; color:#000;">
        ${content.replace(/\n/g, '<br>')}
    </div>
  </div>
`;
