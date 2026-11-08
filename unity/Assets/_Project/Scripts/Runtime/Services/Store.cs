using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace AstroFlowJam
{
    /// <summary>
    /// Gerçek satın alma (Unity IAP 5, App Store). Bütün mağaza çağrıları buradan geçer. Akış: ürün satın alınır →
    /// mağaza onay bekleyen siparişi verir → içerik verilip kaydedilir → sipariş onaylanır. Oyun onaydan önce kapanırsa
    /// sipariş bir sonraki açılışta yeniden gelir; aynı işlem iki kez verilmesin diye işlem kimlikleri saklanır.
    /// Mağaza bağlanamazsa geliştirme derlemesi ürünü test için doğrudan verir; yayın derlemesi oyuncuya bildirir.
    /// </summary>
    public static class Store
    {
        const int RememberedTransactions = 40;

        static PurchasingController controller;
        static string storeName;
        static bool ready;
        static Action<bool, string> purchaseDone;
        static Action<string> restoreDone;

        /// <summary>Ürünler ya da sahip olunanlar değişti (fiyatlar geldi, satın alma ya da geri yükleme): mağaza yenilenir.</summary>
        public static event Action Changed;

        public static bool Ready => ready;

        /// <summary>Oyun açılışında bir kez.</summary>
        public static void Initialize()
        {
            if (controller != null) return;
            try
            {
                storeName = UnityIAPServices.GetDefaultStore();
                controller = new PurchasingController(storeName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Store] Unavailable: {e.Message}");
                controller = null;
                return;
            }
            controller.OnStoreConnected += store => controller.FetchProducts(ShopCatalog.ProductDefinitions(), store);
            controller.OnStoreDisconnected += (store, failure) => { ready = false; Debug.LogWarning($"[Store] Disconnected: {failure.Message}"); };
            controller.OnProductsFetched += (store, products) =>
            {
                ready = products.Exists(p => p.availableToPurchase);   // App Store Connect'te ürün yoksa hazır sayılmaz
                controller.FetchPurchases(store);          // sahip olunan reklam kaldırma (yeniden yüklemede de geri gelir)
                Changed?.Invoke();
            };
            controller.OnProductsFetchFailed += (store, failure) => Debug.LogWarning($"[Store] Products failed: {failure.FailureReason}");
            controller.OnPurchasePending += (store, order) => OnPending(order);
            controller.OnPurchaseConfirmed += (store, order) =>
            {
                if (order is FailedOrder failed) Debug.LogWarning($"[Store] Confirm failed: {failed.FailureReason} {failed.Details}");
            };
            controller.OnPurchaseFailed += (store, failed) =>
            {
                Debug.LogWarning($"[Store] Purchase failed: {failed.FailureReason} {failed.Details}");
                Finish(false, failed.FailureReason == PurchaseFailureReason.UserCancelled ? null : "Purchase failed. Please try again.");
            };
            controller.OnPurchaseDeferred += (store, deferred) => Finish(false, "Waiting for approval");
            controller.OnPurchasesFetched += (store, orders) =>
            {
                foreach (var confirmed in orders.ConfirmedOrders)
                    foreach (var item in confirmed.CartOrdered.Items())
                        if (item.Product?.definition?.id == ShopCatalog.RemoveAdsId && !ShopCatalog.AdsRemoved)
                        {
                            ShopCatalog.GrantProduct(ShopCatalog.RemoveAdsId);
                            Changed?.Invoke();
                        }
            };
            controller.OnPurchasesFetchFailed += (store, failure) => Debug.LogWarning($"[Store] Purchases failed: {failure.Message}");
            _ = controller.Connect();
        }

        /// <summary>App Store'daki fiyat (oyuncunun para birimiyle); mağaza bağlı değilse katalogdaki yedek fiyat.</summary>
        public static string Price(string productId, string fallback)
        {
            var price = ready ? controller.GetProductById(productId, storeName)?.metadata?.localizedPriceString : null;
            return string.IsNullOrEmpty(price) ? fallback : price;
        }

        /// <summary>Ürünü satın alır; sonuçta <paramref name="done"/>(başarılı mı, gösterilecek mesaj — iptalde null).</summary>
        public static void Buy(string productId, Action<bool, string> done)
        {
            if (purchaseDone != null) return;                   // önceki satın alma sürüyor
            if (!ready)
            {
                if (DevTools.Enabled)
                {
                    var message = ShopCatalog.GrantProduct(productId);
                    Changed?.Invoke();
                    done(true, $"Test purchase: {message}");
                }
                else done(false, "Store is not available. Please try again later.");
                return;
            }
            var product = controller.GetProductById(productId, storeName);
            if (product == null || !product.availableToPurchase)
            {
                done(false, "This item is not available right now.");
                return;
            }
            purchaseDone = done;
            controller.PurchaseProduct(product);
        }

        /// <summary>Ayarlar'daki "Restore Purchases": reklam kaldırma gibi kalıcı ürünler geri gelir.</summary>
        public static void Restore(Action<string> done)
        {
            if (!ready) { done("Store is not available. Please try again later."); return; }
            restoreDone = done;
            controller.RestoreTransactions((store, success, error) =>
            {
                var callback = restoreDone;
                restoreDone = null;
                callback?.Invoke(!success ? "Restore failed. Please try again."
                    : ShopCatalog.AdsRemoved ? "Purchases restored" : "No purchases to restore");
            });
        }

        // Onay bekleyen sipariş: içerik verilir ve kaydedilir, sonra onaylanır. Daha önce verilmiş işlem yeniden verilmez.
        static void OnPending(PendingOrder order)
        {
            var transaction = order.Info?.TransactionID;
            string message = null;
            if (!AlreadyGranted(transaction))
            {
                foreach (var item in order.CartOrdered.Items())
                {
                    var id = item.Product?.definition?.id;
                    if (id == null) continue;
                    message = ShopCatalog.GrantProduct(id) ?? message;
                    Telemetry.Purchase(id, GameSave.GetInt(SaveKeys.Progress, 0) + 1);
                }
                RememberGranted(transaction);
            }
            controller.ConfirmPurchase(order);
            Changed?.Invoke();
            Finish(true, message);
        }

        static void Finish(bool success, string message)
        {
            var done = purchaseDone;
            purchaseDone = null;
            done?.Invoke(success, message);
        }

        static bool AlreadyGranted(string transaction) =>
            !string.IsNullOrEmpty(transaction) && Array.IndexOf(GameSave.GetString(SaveKeys.IapTransactions, "").Split('|'), transaction) >= 0;

        static void RememberGranted(string transaction)
        {
            if (string.IsNullOrEmpty(transaction)) return;
            var list = new List<string>(GameSave.GetString(SaveKeys.IapTransactions, "").Split('|', StringSplitOptions.RemoveEmptyEntries)) { transaction };
            if (list.Count > RememberedTransactions) list.RemoveRange(0, list.Count - RememberedTransactions);
            GameSave.SetString(SaveKeys.IapTransactions, string.Join("|", list));
            GameSave.Save();
        }
    }
}
