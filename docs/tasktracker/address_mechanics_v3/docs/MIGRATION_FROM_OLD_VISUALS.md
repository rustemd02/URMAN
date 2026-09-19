# Migration from previous URMAN packs

Этот v3 архив намеренно не содержит:
- GLB адресных табличек;
- PNG/PBR табличек;
- зафиксированной зелёной art direction;
- генератора ржавчины.

Старые визуальные прототипы **не являются dependency**.

Можно удалить их из проекта без влияния на:
- address registry;
- parcel IDs;
- road graph;
- quest references;
- navigation;
- map generation.

Astra 6 должна создавать новый visual layer через адаптер:
`AddressRecord -> SignVisualComponent`.
