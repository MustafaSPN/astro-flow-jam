using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Tahta zemini, çerçeve ve renkli kapıların görünümü; hücre ↔ dünya dönüşümü de buradadır.
    /// Kenarlar fiziksel olarak kesintisizdir: kapı parçaları da katıdır, roket kapıdan fizikle
    /// geçmez; uygun roket kapı eşiğine gelince kapı onu animasyonla içine alır.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        /// <summary>Duvar kalınlığı (AFJ_Wall_Straight_1Unit gövdesi ±0,2); duvarın iç yüzü tahta kenarındadır.</summary>
        public const float FrameThickness = 0.5f;   // yuvarlak duvar kitinin kalınlığı (±0,25)

        BoardModel model;
        readonly List<GateView> gates = new();
        readonly List<AsteroidView> asteroids = new();

        /// <summary>Asteroitler, tahta modelindeki sırayla.</summary>
        public IReadOnlyList<AsteroidView> Asteroids => asteroids;

        public Vector3 Origin => transform.position;

        /// <summary>Kapılar, bölüm verisindeki çıkışlarla aynı sıradadır.</summary>
        public IReadOnlyList<GateView> Gates => gates;

        public void Build(BoardModel board, PrototypePalette palette, ModelLibrary models)
        {
            model = board;
            foreach (Transform child in transform) Destroy(child.gameObject);
            gates.Clear();

            // Zemin: hücre başına bir karo. Karo, blok ve duvar modelleri aynı zemin seviyesinde buluşur.
            var floor = new GameObject("Floor").transform;
            floor.SetParent(transform, false);
            // Delik hücrelerde karo yok: duvar deliğin etrafından dolaşır (BuildOutline).
            for (var x = 0; x < board.Width; x++)
                for (var y = 0; y < board.Height; y++)
                    if (!board.IsHole(new Vector2Int(x, y)))
                        ModelKit.Spawn(models.floorTile, floor, new Vector3(x + 0.5f, y + 0.5f, ModelKit.GroundOffset), Quaternion.identity, Color.white);

            // Duvarlar oyun renginden bağımsızdır: pembe gövde tahta çerçevesi rengine boyanır, krem ve koyu detaylar sabit.
            var wallColor = palette.boardFrame.GetColor(ShaderIds.BaseColor);
            var walls = new GameObject("Wall Models").transform;
            walls.SetParent(transform, false);
            BuildOutline(walls, floor, models, wallColor);

            BuildWalls();

            // Asteroitler: zeminin üstünde, kendi katı hücreleriyle.
            asteroids.Clear();
            for (var k = 0; k < board.AsteroidCount; k++)
            {
                var view = new GameObject($"Asteroid {k}").AddComponent<AsteroidView>();
                view.transform.SetParent(transform, false);
                view.Build(board.GetAsteroid(k), models);
                asteroids.Add(view);
            }

            // Seçili bloğun halesi için maske: yalnızca tahtanın iç alanında görünür, duvarların dışına taşmaz.
            var maskGo = new GameObject("Halo Mask");
            maskGo.transform.SetParent(transform, false);
            maskGo.transform.localPosition = new Vector3(board.Width / 2f, board.Height / 2f, 0f);
            var mask = maskGo.AddComponent<SpriteMask>();
            mask.sprite = UiArt.Box(16, 16, 0f, Color.white, Color.white, 0f);
            var unit = mask.sprite.bounds.size;
            maskGo.transform.localScale = new Vector3(board.Width / unit.x, board.Height / unit.y, 1f);

            for (var i = 0; i < board.Exits.Count; i++)
            {
                var gate = new GameObject($"Gate {i}").AddComponent<GateView>();
                gate.transform.SetParent(transform, false);
                gate.Build(board.Exits[i], board.Width, board.Height, palette, models);
                gates.Add(gate);
            }

            // Duvarlar ve kapıların kenar parçaları (direk, kapak, eşik) koyu gri-lacivert; kapının renkli kapakları,
            // ışıkları ve okları korunur.
            Paint(walls, WallColor, WallSmoothness);
            foreach (var g in gates) Paint(g.transform, WallColor, WallSmoothness, keepGateParts: true);
        }


        static readonly Color WallColor = new(0.17f, 0.2f, 0.29f);   // koyu gri ile lacivert arası
        public const float WallSmoothness = 0.8f;

        static void Paint(Transform root, Color color, float smoothness, bool keepGateParts = false)
        {
            var copies = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (keepGateParts && r.name.StartsWith("Direction_Chevron")) continue;
                var mats = r.sharedMaterials;
                for (var m = 0; m < mats.Length; m++)
                {
                    if (!mats[m]) continue;
                    // Kapının renkli parçaları (blok malzemesi, renk ve ışık şeritleri) boyanmaz.
                    if (keepGateParts && (mats[m].name.StartsWith("Gate_Color_Changeable") || mats[m].name.StartsWith("Gate_Light_Changeable")
                                          || mats[m].name.StartsWith("AFJ Coral") || mats[m].name.StartsWith("Gate_Joint_Metal") || mats[m].name.StartsWith("Gate_Arrow_Changeable"))) continue;   // kol halkaları metalik gri kalır
                    if (!copies.TryGetValue(mats[m], out var copy))
                    {
                        copy = new Material(mats[m]) { name = mats[m].name + " (duvar)" };
                        copy.SetColor(ShaderIds.BaseColor, color);
                        copy.SetFloat(ShaderIds.Smoothness, smoothness);
                        copies[mats[m]] = copy;
                    }
                    mats[m] = copy;
                }
                r.sharedMaterials = mats;
            }
        }


        // Oynanabilir hücrelerin çevresi duvar olur: tahta dışına ya da bir deliğe bakan her hücre kenarına, dışarı (deliğin
        // içine) doğru FrameThickness kalınlığında duvar. Aynı hizadaki ardışık kenarlar tek parça duvardır (modül boyuna
        // gerilir, birleşme çizgisi görünmez); kapılar dış kenardaki duvar çizgisini tam hücre sınırında keser.
        // Her köşeye köşe modülü gelir: dışa dönük köşede (köşe noktasını çevreleyen dört hücreden biri oynanabilir) duvarların
        // ucunun ötesine, içe dönük köşede (üçü oynanabilir) iki duvarın buluştuğu kareye; içe dönük köşede biten düz duvar
        // köşe karesi kadar kısalır, hiçbir parça üst üste binmez. Dar çentiğin ucunda böylece yan yana iki köşe modülü olur.
        void BuildOutline(Transform parent, Transform floor, ModelLibrary models, Color color)
        {
            int W = model.Width, H = model.Height;
            bool Open(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && !model.IsHole(new Vector2Int(x, y));
            int OpenAround(int vx, int vy) => (Open(vx - 1, vy - 1) ? 1 : 0) + (Open(vx, vy - 1) ? 1 : 0) + (Open(vx - 1, vy) ? 1 : 0) + (Open(vx, vy) ? 1 : 0);
            bool Concave(int vx, int vy) => OpenAround(vx, vy) == 3;
            var normals = new[] { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right };

            // Kenar: (hücre, dış normal). Duvar gerekenler: komşu oynanamaz ve orada kapı yok.
            bool NeedsWall(int x, int y, Vector2Int n)
            {
                if (!Open(x, y) || Open(x + n.x, y + n.y)) return false;
                var outer = x + n.x < 0 || y + n.y < 0 || x + n.x >= W || y + n.y >= H;
                if (!outer) return true;
                var edge = n == Vector2Int.down ? Edge.Bottom : n == Vector2Int.up ? Edge.Top : n == Vector2Int.left ? Edge.Left : Edge.Right;
                return !HasGateAt(edge, n.x == 0 ? x : y);
            }

            var t = FrameThickness / 2f;
            foreach (var n in normals)
            {
                var horizontal = n.x == 0;                               // alt/üst kenarlar x boyunca uzanır
                var lines = horizontal ? H : W;
                var length = horizontal ? W : H;
                for (var line = 0; line < lines; line++)
                {
                    var i = 0;
                    while (i < length)
                    {
                        int X(int k) => horizontal ? k : line;
                        int Y(int k) => horizontal ? line : k;
                        if (!NeedsWall(X(i), Y(i), n)) { i++; continue; }
                        var start = i;
                        while (i < length && NeedsWall(X(i), Y(i), n)) i++;
                        // Duvar orta çizgisi: kenarın t kadar dışı. Uçlar içe dönük köşedeyse köşe karesi kadar kısalır.
                        var edgeLine = horizontal ? line + (n.y > 0 ? 1 : 0) : line + (n.x > 0 ? 1 : 0);
                        float from = start, to = i;
                        if (horizontal ? Concave(start, edgeLine) : Concave(edgeLine, start)) from += FrameThickness;
                        if (horizontal ? Concave(i, edgeLine) : Concave(edgeLine, i)) to -= FrameThickness;
                        if (to - from < 0.01f) continue;
                        var mid = (from + to) / 2f;
                        var center = horizontal ? new Vector3(mid, edgeLine + n.y * t, 0f) : new Vector3(edgeLine + n.x * t, mid, 0f);
                        var segment = new GameObject($"Wall {n} {line}:{start}-{i - 1}").transform;
                        segment.SetParent(parent, false);
                        segment.localPosition = center + new Vector3(0f, 0f, ModelKit.GroundOffset);
                        segment.localRotation = horizontal ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
                        segment.localScale = new Vector3(to - from, 1f, 1f);
                        ModelKit.Spawn(models.wallStraight, segment, Vector3.zero, Quaternion.identity, color);
                    }
                }
            }

            // Köşe modülü (AFJ_Simple_Wall_Corner) yerel x [−0,5; 0], y [−0,25; 0,25]; yuvarlak tarafı sol alta bakar ve köşe
            // karesinin merkezine oturacak şekilde döndürülür. Yuvarlak taraf duvarın dönüşünün dış yanıdır: dışa dönük köşede
            // oynanabilir alandan uzağa, içe dönük köşede (kare deliğin içinde) köşe noktasına, yani oynanabilir alana bakar.
            for (var vx = 0; vx <= W; vx++)
                for (var vy = 0; vy <= H; vy++)
                {
                    bool bl = Open(vx - 1, vy - 1), br = Open(vx, vy - 1), tl = Open(vx - 1, vy), tr = Open(vx, vy);
                    var open = OpenAround(vx, vy);
                    Vector2 diag;   // köşe karesinin köşe noktasına göre yönü
                    if (open == 1) diag = bl ? new Vector2(1f, 1f) : br ? new Vector2(-1f, 1f) : tl ? new Vector2(1f, -1f) : new Vector2(-1f, -1f);
                    else if (open == 3) diag = !bl ? new Vector2(-1f, -1f) : !br ? new Vector2(1f, -1f) : !tl ? new Vector2(-1f, 1f) : new Vector2(1f, 1f);
                    else continue;
                    var round = open == 1 ? diag : -diag;   // yuvarlak tarafın baktığı çapraz
                    var angle = round.x < 0 ? (round.y < 0 ? 0f : 270f) : (round.y < 0 ? 90f : 180f);
                    var rotation = Quaternion.Euler(0f, 0f, angle);
                    var square = new Vector3(vx + diag.x * t, vy + diag.y * t, 0f);
                    var pivot = square + rotation * new Vector3(0.25f, 0f, 0f);
                    ModelKit.Spawn(models.wallCorner, parent, new Vector3(pivot.x, pivot.y, ModelKit.GroundOffset), rotation, color);
                    // İçe dönük köşede yuvarlak yüzün önünde (deliğin içindeki karede) zemin karosu yoktu: köşe karesi kadar
                    // zemin konur; çoğu duvarın altında kalır, yalnız cebi doldurur, duvarın dışına taşmaz.
                    if (open == 3)
                    {
                        var tile = ModelKit.Spawn(models.floorTile, floor, new Vector3(square.x, square.y, ModelKit.GroundOffset), Quaternion.identity, Color.white);
                        // Model kabı Blender eksenlerinde: yerel x sahne x, yerel z sahne y, yerel y yükseklik.
                        tile.localScale = Vector3.Scale(tile.localScale, new Vector3(FrameThickness, 1f, FrameThickness));
                    }
                }
        }

        // Tahtanın dört yanında, kenardan başlayıp dışarı uzanan kalın katı kutular.
        void BuildWalls()
        {
            const float thick = 2f;
            var walls = new GameObject("Walls");
            walls.transform.SetParent(transform, false);
            void Box(Vector2 center, Vector2 size)
            {
                var c = walls.AddComponent<BoxCollider2D>();
                c.offset = center;
                c.size = size;
            }
            float w = model.Width, h = model.Height;
            Box(new Vector2(w / 2f, -thick / 2f), new Vector2(w + thick * 2f, thick));
            Box(new Vector2(w / 2f, h + thick / 2f), new Vector2(w + thick * 2f, thick));
            Box(new Vector2(-thick / 2f, h / 2f), new Vector2(thick, h + thick * 2f));
            Box(new Vector2(w + thick / 2f, h / 2f), new Vector2(thick, h + thick * 2f));
            // Delikler katıdır: bloklar kenarlarından kayar, içine giremez.
            for (var x = 0; x < model.Width; x++)
                for (var y = 0; y < model.Height; y++)
                    if (model.IsHole(new Vector2Int(x, y))) Box(new Vector2(x + 0.5f, y + 0.5f), Vector2.one);
        }



        bool HasGateAt(Edge edge, int index)
        {
            foreach (var e in model.Exits)
                if (e.edge == edge && index >= e.start && index < e.start + e.width)
                    return true;
            return false;
        }

        public static Vector3 EdgeSegmentCenter(Edge edge, int index, int width, int height) => edge switch
        {
            Edge.Bottom => new Vector3(index + 0.5f, -FrameThickness / 2, 0f),
            Edge.Top => new Vector3(index + 0.5f, height + FrameThickness / 2, 0f),
            Edge.Left => new Vector3(-FrameThickness / 2, index + 0.5f, 0f),
            _ => new Vector3(width + FrameThickness / 2, index + 0.5f, 0f),
        };

        public Vector3 CellToWorld(Vector2 cellPosition) => Origin + new Vector3(cellPosition.x, cellPosition.y, 0f);

        public Vector2 WorldToCellSpace(Vector3 world) => new(world.x - Origin.x, world.y - Origin.y);

        public Vector2Int WorldToCell(Vector3 world)
        {
            var p = WorldToCellSpace(world);
            return new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
        }

        public void ShakeGate(Dir dir, Vector2Int rocketOrigin, Vector2Int rocketSize)
        {
            foreach (var g in GatesFacing(dir, rocketOrigin, rocketSize)) g.Shake();
        }

        /// <summary>Roket bu yöndeki kenara dayandığında karşısında kalan kapılar.</summary>
        public IEnumerable<GateView> GatesFacing(Dir dir, Vector2Int rocketOrigin, Vector2Int rocketSize)
        {
            var edge = dir.ToEdge();
            var vertical = edge == Edge.Top || edge == Edge.Bottom;
            var spanStart = vertical ? rocketOrigin.x : rocketOrigin.y;
            var spanEnd = spanStart + (vertical ? rocketSize.x : rocketSize.y);
            foreach (var g in gates)
                if (g.Spec.edge == edge && g.Spec.start < spanEnd && spanStart < g.Spec.start + g.Spec.width)
                    yield return g;
        }
    }
}
