// iOS reklam takibi izni (App Tracking Transparency) köprüsü: durum okuma ve sistem izin penceresini açma.
#import <AppTrackingTransparency/AppTrackingTransparency.h>

// 0: henüz sorulmadı, 1: kısıtlı, 2: reddedildi, 3: izin verildi (iOS 14 öncesinde izin gerekmez: 3).
extern "C" int AFJ_TrackingStatus()
{
    if (@available(iOS 14, *)) return (int)[ATTrackingManager trackingAuthorizationStatus];
    return 3;
}

extern "C" void AFJ_RequestTracking()
{
    if (@available(iOS 14, *))
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {}];
}
