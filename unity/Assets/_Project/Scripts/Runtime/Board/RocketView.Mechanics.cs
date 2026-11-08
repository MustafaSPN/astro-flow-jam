using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class RocketView : MonoBehaviour
    {
        // ------------------------------------------------------------ tahta mekanikleri
        // Blok özellikleri (Blender: Block_Features) blok üst yüzünün hemen önünde, model kabının içinde durur; seçilince
        // blokla birlikte kalkar. Anahtar tek model, ok kodla çizilen sprite (MechanicArt.Arrow); kilit şekle göre kurulur:
        // tek hücrede hazır çapraz kilit, düz bloklarda ortada göbek ve iki uca kayış, L/T ve diğer şekillerde en çok komşulu
        // hücrede göbek ve dolu hücreler boyunca düz kayışlar (boş hücreyi çapraz geçmez; gerekirse dirsek bağlantısıyla
        // devam eder). Buz kodla çizilir (MechanicArt): sayaç azaldıkça çatlaklar artar, sıfırda kırılıp dağılır. Kilit
        // açılınca göbek fırlar, kayışlar göbeğe doğru kısalır, uç bağlantıları küçülür.
        const float FeatureDepth = -0.285f;    // blok üst yüzü Blender z 0,275; eklentiler 0,285'te (sahnede −z kameraya doğru)
        const float IceDepth = -0.3f;
        const float EndReach = 0.2f;           // kayış uç hücrenin merkezinden bu kadar ileride biter (bloğun ucundan önce)
        const float HubScale = 1.3f;           // kilit göbeği tasarımdakinden biraz büyük
        const float CounterGrow = 1.5f;        // buz sayacı roket sayacından büyük
        // Kilit ve anahtar ödül kapısındaki kilidin malzemelerinde (RewardDoor: AFJ_MagneticLock_*, renk ve pürüzsüzlük aynı):
        // lacivert gövde, kayışlar ve anahtar, koyu kilit yüzü, camgöbeği neon simge, anahtar içi ve kayış uçları.
        static readonly Color LockNavy = SceneArt.Hex(0x182640), LockFace = SceneArt.Hex(0x102139),
                              LightMetal = SceneArt.Hex(0xC3CDDC);
        const float NavySmooth = 0.75f, FaceSmooth = 0.6f, LightMetalSmooth = 0.92f;
        ModelLibrary featureModels;
        int iceInitial;
        readonly System.Collections.Generic.List<(SpriteRenderer sprite, int connected, int variant)> iceCells = new();
        readonly System.Collections.Generic.List<Transform> features = new();
        // Kayış ucu bağlantıları: açılırken 'from' (yerinden) 'to' (kayışın kilit tarafındaki başlangıcı) noktasına kayar.
        readonly System.Collections.Generic.List<(Transform anchor, Vector3 from, Vector3 to)> lockAnchors = new();
        readonly System.Collections.Generic.List<(Transform arm, Vector3 scale, bool along)> lockArms = new();   // along: yalnız boyu kısalır
        Transform lockRoot, lockHub;
        Vector3 hubBaseScale = Vector3.one;
        UnityEngine.UI.Text iceText;
        GameObject iceTextRoot;
        float iceBreakTime = -1f, lockBreakTime = -1f;
        const float BreakSeconds = 0.35f;
        const float StrapRetractSeconds = 0.3f, HubSwellSeconds = 0.08f;   // kilit açılması: kayışlar toplanır, göbek şişip patlar
        static readonly System.Collections.Generic.Dictionary<Color, Material> NeonCache = new();

        Vector3 CellCenter(Vector2Int c, float z) => new(c.x + 0.5f - Spec.size.x / 2f, c.y + 0.5f - Spec.size.y / 2f, z);

        // İşaretlerin (ok, anahtar, buz sayacı) yeri: dikdörtgen bloklarda ortası; L/T gibi şekillerde ortaya en yakın dolu hücre
        // (eşitlikte en çok komşulu) – çevreleyen kutunun ortası boş hücreye ya da kenara düşebilir.
        /// <summary>İşaret hücresinin (sayı, ok, anahtar) blok merkezine göre konumu (tahta birimi).</summary>
        public Vector2 MarkerOffset => MarkerPoint(0f);

        Vector3 MarkerPoint(float z)
        {
            var spec = Spec;
            if (spec.shape is not { Count: > 0 }) return new Vector3(0f, 0f, z);
            var cells = new System.Collections.Generic.HashSet<Vector2Int>(spec.LocalCells);
            var mid = (Vector2)spec.size / 2f - new Vector2(0.5f, 0.5f);
            Vector2Int best = default; var bestScore = float.MaxValue;
            foreach (var c in cells)
            {
                var n = (cells.Contains(c + Vector2Int.left) ? 1 : 0) + (cells.Contains(c + Vector2Int.right) ? 1 : 0)
                      + (cells.Contains(c + Vector2Int.down) ? 1 : 0) + (cells.Contains(c + Vector2Int.up) ? 1 : 0);
                var score = ((Vector2)c - mid).sqrMagnitude - n * 0.01f;
                if (score < bestScore) { bestScore = score; best = c; }
            }
            return CellCenter(best, z);
        }

        void BuildMechanics(ModelLibrary models)
        {
            featureModels = models;
            var spec = Spec;
            var center = MarkerPoint(FeatureDepth);
            if (spec.ice > 0) BuildIce();
            if (spec.lockGroup > 0 && models && models.lockHub) BuildLock();
            var crowded = spec.ice > 0 || spec.lockGroup > 0;
            if (spec.keyGroup > 0 && models && models.keyEmblem)
            {
                // Buz ya da kilit varsa anahtar sağ üst köşeye küçük konur; yoksa ortada durur.
                var pos = crowded ? new Vector3(spec.size.x / 2f - 0.27f, spec.size.y / 2f - 0.27f, FeatureDepth - 0.02f) : center;
                var key = Feature(models.keyEmblem, "Key", pos, 0f, crowded ? new Vector3(0.6f, 0.6f, 1f) : Vector3.one);
                PaintLock(key);
            }
            if (spec.axis != MoveAxis.Free)
            {
                var vertical = spec.axis == MoveAxis.Vertical;
                // Ok, hareket eksenindeki en uzun dolu hücre dizisinde durur ve o dizi boyunca uzar (uçlarda ~0,35 hücre pay,
                // enine kalınlık aynı). Düz blokta dizi bloğun tamamı, L/T'de en uzun kol; tek hücrede ok olduğu gibi kalır.
                var (runCenter, runLength) = LongestRun(vertical);
                var position = runLength > 1 ? new Vector3(runCenter.x, runCenter.y, FeatureDepth) : center;
                if (spec.keyGroup > 0) position += vertical ? new Vector3(-0.24f, 0f, 0f) : new Vector3(0f, -0.24f, 0f);
                var length = runLength > 1 ? Mathf.Max(ArrowLength1x1, runLength - ArrowEndGap * 2f) : ArrowLength1x1;
                BuildArrow(position, vertical ? 90f : 0f, length);
            }
        }

        const float ArrowLength1x1 = 0.66f;    // tek hücrede okun boyu
        const float ArrowEndGap = 0.3f;        // uzayan okun dizinin uçlarında bıraktığı pay (hücre)

        // Ok: kodla çizilen çift başlı sprite (MechanicArt.Arrow); kalınlık ve uçlar sabit, gövde boyla uzar.
        Transform BuildArrow(Vector3 position, float angle, float length)
        {
            var r = SceneArt.Place(MechanicArt.Arrow(length), "Axis Arrow", model, position, 6);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            features.Add(r.transform);
            return r.transform;
        }

        // Hareket eksenindeki en uzun dolu hücre dizisi: ortası (model yerel) ve hücre sayısı. Eşitlikte bloğun ortasına yakını.
        (Vector2 center, int length) LongestRun(bool vertical)
        {
            var cells = new System.Collections.Generic.HashSet<Vector2Int>(Spec.LocalCells);
            var step = vertical ? Vector2Int.up : Vector2Int.right;
            var mid = (Vector2)Spec.size / 2f;
            (Vector2 center, int length) best = (Vector2.zero, 0);
            var bestDistance = float.MaxValue;
            foreach (var c in cells)
            {
                if (cells.Contains(c - step)) continue;                 // yalnız dizinin başından say
                var n = 1;
                while (cells.Contains(c + step * n)) n++;
                var runCenter = (Vector2)c + new Vector2(0.5f, 0.5f) + (Vector2)step * ((n - 1) / 2f);
                var distance = (runCenter - mid).sqrMagnitude;
                if (n > best.length || (n == best.length && distance < bestDistance))
                {
                    best = (runCenter - mid, n);
                    bestDistance = distance;
                }
            }
            return best;
        }

        // Özellik modeli: sahnedeki dönüşü (z ekseni) ve ölçeği bir ara kaptan verilir; model kabı Blender eksenlerindedir.
        Transform Feature(GameObject prefab, string name, Vector3 position, float angle, Vector3 scale, Transform parent = null)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent ? parent : model, false);
            holder.localPosition = position;
            holder.localRotation = Quaternion.Euler(0f, 0f, angle);
            holder.localScale = scale;
            ModelKit.Spawn(prefab, holder, Vector3.zero, Quaternion.identity, Color.white);
            if (!parent) features.Add(holder);
            return holder;
        }

        // Kilit/anahtar boyası: ödül kapısındaki kilidin paleti (parça adına göre: kilit yüzü koyu, kayış çelik mavisi).
        void PaintLock(Transform root)
        {
            if (!NeonCache.TryGetValue(SceneArt.Cyan, out var neon) || !neon)
                NeonCache[SceneArt.Cyan] = neon = featureModels && featureModels.neonMaterial
                    ? ConveyorTrack.Neon(featureModels.neonMaterial, SceneArt.Cyan) : ModelKit.Lit("Feature_Cyan", SceneArt.Cyan);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    var name = mats[i].name;
                    // Anahtar (Direction_*): kenar kilit yüzünün koyu tonunda, yüz kilidin lacivertinde (neon yayla birlikte kilit gibi).
                    if (name.StartsWith("Direction_"))
                    {
                        mats[i] = name.StartsWith("Direction_Black")
                            ? ModelKit.Lit("Lock Face", LockFace, FaceSmooth) : ModelKit.Lit("Lock Navy", LockNavy, NavySmooth);
                        continue;
                    }
                    // Açık metalik: kayışın metal kenarı ve kilidin en dış katmanı (gövde kabuğu).
                    if (name.StartsWith("Lock_Strap_Light_Metal") || r.name.StartsWith("Lock_Housing"))
                    {
                        mats[i] = ModelKit.Lit("Lock Light Metal", LightMetal, LightMetalSmooth);
                        continue;
                    }
                    if (!name.StartsWith("Feature_")) continue;
                    if (name.StartsWith("Feature_Cyan") || r.name.StartsWith("Anchor_Cap")) { mats[i] = neon; continue; }   // kayış uçları neon
                    if (r.name.StartsWith("Lock_Face")) mats[i] = ModelKit.Lit("Lock Face", LockFace, FaceSmooth);
                    else mats[i] = ModelKit.Lit("Lock Navy", LockNavy, NavySmooth);    // kilit gövdesi, kayışlar, uç tabanları, anahtar gövdesi
                }
                r.sharedMaterials = mats;
            }
        }

        void BuildLock()
        {
            var spec = Spec;
            var cells = new System.Collections.Generic.HashSet<Vector2Int>(spec.LocalCells);
            lockRoot = new GameObject("Lock").transform;
            lockRoot.SetParent(model, false);
            features.Add(lockRoot);
            if (cells.Count == 1)
            {
                foreach (var c in cells)
                {
                    var whole = Feature(featureModels.lock1x1, "Lock 1x1", CellCenter(c, FeatureDepth), 0f, Vector3.one, lockRoot);
                    foreach (var t in whole.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.StartsWith("Arm_")) lockArms.Add((t, t.localScale, false));
                        else if (t.name.StartsWith("Anchor_")) lockAnchors.Add((t, t.localPosition, Vector3.zero));   // merkeze toplanır
                        else if (t.name.StartsWith("Lock_Hub")) { lockHub = t; t.localScale *= HubScale; hubBaseScale = t.localScale; }
                    }
                }
                PaintLock(lockRoot);
                return;
            }

            Vector3 P(Vector2 cell) => new(cell.x + 0.5f - spec.size.x / 2f, cell.y + 0.5f - spec.size.y / 2f, FeatureDepth);
            // Kayış 'from' noktasından uç hücreye, onun da ötesinde bloğun ucuna kadar uzanır; uçta bağlantı.
            void Arm(Vector3 from, Vector3 endCell, bool anchorAtEnd)
            {
                var d = endCell - from;
                if (d.sqrMagnitude < 1e-4f) return;
                d += d.normalized * EndReach;
                var length = d.magnitude;
                var arm = Feature(featureModels.lockArm, "Arm", from, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, new Vector3(length, 1f, 1f), lockRoot);
                lockArms.Add((arm, arm.localScale, true));
                if (anchorAtEnd && featureModels.lockAnchor)
                {
                    var end = from + d.normalized * length;
                    lockAnchors.Add((Feature(featureModels.lockAnchor, "Anchor", end, 0f, Vector3.one, lockRoot), end, from));
                }
            }

            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            int xs = 0, ys = 0;
            var first = default(Vector2Int);
            foreach (var c in cells) { first = c; break; }
            foreach (var c in cells) { if (c.x != first.x) xs++; if (c.y != first.y) ys++; }
            Vector3 hubPoint;
            var reached = new System.Collections.Generic.HashSet<Vector2Int>();
            if (xs == 0 || ys == 0)
            {
                // Düz blok: göbek ortada, iki uç hücreye kayış.
                Vector2Int lo = first, hi = first;
                foreach (var c in cells) { if (c.x + c.y < lo.x + lo.y) lo = c; if (c.x + c.y > hi.x + hi.y) hi = c; }
                hubPoint = (P(lo) + P(hi)) / 2f;
                Arm(hubPoint, P(lo), true);
                Arm(hubPoint, P(hi), true);
                reached.UnionWith(cells);
            }
            else
            {
                // Göbek en çok komşusu olan hücrede (eşitlikte ağırlık merkezine en yakın).
                var centroid = Vector2.zero;
                foreach (var c in cells) centroid += (Vector2)c;
                centroid /= cells.Count;
                var hub = first; var best = -1f;
                // L'de köşe hücresi (dik açılı iki komşu) seçilir; T'de üç komşulu orta hücre.
                foreach (var c in cells)
                {
                    var n = 0;
                    foreach (var d in dirs) if (cells.Contains(c + d)) n++;
                    var horizontal = cells.Contains(c + Vector2Int.left) || cells.Contains(c + Vector2Int.right);
                    var vertical = cells.Contains(c + Vector2Int.up) || cells.Contains(c + Vector2Int.down);
                    var score = n * 10f + (horizontal && vertical ? 5f : 0f) - ((Vector2)c - centroid).sqrMagnitude;
                    if (score > best) { best = score; hub = c; }
                }
                hubPoint = P(hub);
                reached.Add(hub);
                // Göbekten her yöne dolu hücreler boyunca düz kayış; kalan hücrelere ulaşılan hücreden dirsekle devam.
                var frontier = new System.Collections.Generic.List<Vector2Int> { hub };
                while (frontier.Count > 0)
                {
                    var next = new System.Collections.Generic.List<Vector2Int>();
                    foreach (var from in frontier)
                        foreach (var d in dirs)
                        {
                            var k = 0;
                            while (cells.Contains(from + d * (k + 1)) && !reached.Contains(from + d * (k + 1))) k++;
                            if (k == 0) continue;
                            for (var i = 1; i <= k; i++) { reached.Add(from + d * i); next.Add(from + d * i); }
                            var elbow = from != hub;
                            if (elbow && featureModels.lockAnchor)
                                lockAnchors.Add((Feature(featureModels.lockAnchor, "Elbow", P(from), 0f, Vector3.one, lockRoot), P(from), P(hub)));
                            Arm(P(from), P(from + d * k), true);
                        }
                    frontier = next;
                }
            }
            lockHub = Feature(featureModels.lockHub, "Hub", hubPoint + new Vector3(0f, 0f, -0.005f), 0f, Vector3.one * HubScale, lockRoot);
            hubBaseScale = lockHub.localScale;
            PaintLock(lockRoot);
        }

        void BuildIce()
        {
            var spec = Spec;
            iceInitial = spec.ice;
            var cells = new System.Collections.Generic.HashSet<Vector2Int>(spec.LocalCells);
            var stage = MechanicArt.IceStage(iceInitial, iceInitial);
            var index = 0;
            foreach (var c in cells)
            {
                var connected = (cells.Contains(c + Vector2Int.left) ? 1 : 0) | (cells.Contains(c + Vector2Int.right) ? 2 : 0)
                              | (cells.Contains(c + Vector2Int.down) ? 4 : 0) | (cells.Contains(c + Vector2Int.up) ? 8 : 0);
                var variant = (index++ * 3 + spec.origin.x + spec.origin.y) % 4;
                var r = SceneArt.Place(MechanicArt.Ice(stage, connected, variant), "Ice", model, CellCenter(c, IceDepth), 5);
                iceCells.Add((r, connected, variant));
            }
            iceTextRoot = new GameObject("Ice Counter", typeof(RectTransform), typeof(Canvas));
            iceTextRoot.transform.SetParent(model, false);
            iceTextRoot.transform.localPosition = MarkerPoint(IceDepth - 0.12f);
            iceTextRoot.transform.localScale = Vector3.one * CounterScale * 1.25f * CounterGrow;
            var canvas = iceTextRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 8;
            ((RectTransform)iceTextRoot.transform).sizeDelta = new Vector2(160f, 100f);
            iceText = NewText(iceTextRoot.transform);
            iceText.text = spec.ice.ToString();
        }

        /// <summary>Buz sayacı azaldı ya da kilit açıldı: sayaç ve çatlaklar güncellenir; buz eridiyse / kilit açıldıysa kırılır.</summary>
        public void UpdateFrozen(int iceLeft, bool locked)
        {
            if (iceText) iceText.text = iceLeft.ToString();
            if (iceCells.Count > 0 && iceLeft > 0)
            {
                var stage = MechanicArt.IceStage(iceInitial, iceLeft);
                foreach (var (sprite, connected, variant) in iceCells) sprite.sprite = MechanicArt.Ice(stage, connected, variant);
                PunchScale(new Vector3(0.12f, 0.12f, 0.12f), 0.25f, 6, 0.5f);
            }
            if (iceCells.Count > 0 && iceLeft <= 0 && iceBreakTime < 0f)
            {
                iceBreakTime = 0f;
                if (iceTextRoot) iceTextRoot.SetActive(false);
                Haptics.Select();
            }
            if (lockRoot && !locked && lockBreakTime < 0f) { lockBreakTime = 0f; Haptics.Select(); }
        }

        /// <summary>Donuk blok dokunulunca: kısa sarsıntı (oynamaz).</summary>
        public void FrozenTap() => ShakeScale(0.3f, 0.12f, 12);

        void UpdateMechanics(float dt)
        {
            if (iceBreakTime >= 0f)
            {
                iceBreakTime += dt;
                var k = Mathf.Clamp01(iceBreakTime / BreakSeconds);
                foreach (var (sprite, _, _) in iceCells)
                {
                    sprite.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, k);
                    sprite.color = new Color(1f, 1f, 1f, 1f - k);
                }
                if (k >= 1f) { foreach (var (sprite, _, _) in iceCells) Destroy(sprite.gameObject); iceCells.Clear(); iceBreakTime = -1f; }
            }
            if (lockBreakTime >= 0f)
            {
                lockBreakTime += dt;
                // 1) Kayışlar kilidin altına toplanır: kollar başlangıç pivotlarına doğru kısalır, uç bağlantıları kayışın ucuyla
                //    birlikte kilide doğru kayar ve varınca küçülüp kaybolur.
                var retract = Tween.Evaluate(Tween.Ease.InOutSine, Mathf.Clamp01(lockBreakTime / StrapRetractSeconds));
                foreach (var (arm, scale, along) in lockArms)
                    if (arm) arm.localScale = along ? new Vector3(scale.x * (1f - retract), scale.y, scale.z) : scale * (1f - retract);
                foreach (var (anchor, from, to) in lockAnchors)
                {
                    if (!anchor) continue;
                    anchor.localPosition = Vector3.Lerp(from, to, retract);
                    anchor.localScale = Vector3.one * Mathf.Clamp01((1f - retract) / 0.2f);
                }
                // 2) Ardından göbek bir an şişer ve patlar (asteroit gibi): neon kıvılcım, beyaz parlama, lacivert/metal/neon kırıklar.
                var swell = Mathf.Clamp01((lockBreakTime - StrapRetractSeconds) / HubSwellSeconds);
                if (lockHub && swell > 0f) lockHub.localScale = hubBaseScale * (1f + 0.3f * swell);
                if (swell >= 1f)
                {
                    ExplodeLock();
                    Destroy(lockRoot.gameObject); features.Remove(lockRoot); lockRoot = null; lockBreakTime = -1f;
                }
            }
        }

        void ExplodeLock()
        {
            var at = lockHub ? lockHub.position : lockRoot.position;
            at.z -= 0.3f;
            var parent = transform.parent ? transform.parent : transform;
            if (featureModels && featureModels.flameMaterial)
            {
                RewardDoor.Explosion(at, featureModels.flameMaterial, SceneArt.Cyan, parent, 0.55f);
                RewardDoor.Explosion(at, featureModels.flameMaterial, Color.white, parent, 0.35f);
            }
            NeonCache.TryGetValue(SceneArt.Cyan, out var neon);
            var mats = new[]
            {
                ModelKit.Lit("Lock Navy", LockNavy, NavySmooth), ModelKit.Lit("Lock Light Metal", LightMetal, LightMetalSmooth),
                neon ? neon : ModelKit.Lit("Feature_Cyan", SceneArt.Cyan),
            };
            ShatterFx.Create(parent).Shards(at, 0.12f, 12, mats, 0.09f, 4f, 0.55f);
            Haptics.Select();
        }

        /// <summary>Kapıdan geçerken mekanik işaretleri gizlenir.</summary>
        public void HideMechanics()
        {
            foreach (var f in features) if (f) f.gameObject.SetActive(false);
            foreach (var (sprite, _, _) in iceCells) if (sprite) sprite.enabled = false;
            if (iceTextRoot) iceTextRoot.SetActive(false);
        }
    }
}
