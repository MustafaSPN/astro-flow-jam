// iOS dokunsal geri bildirim köprüsü: UIImpactFeedbackGenerator ile şiddet ayarlı tek vuruş.
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *generators[3];

extern "C" void AFJ_HapticImpact(int style, float intensity)
{
    if (style < 0 || style > 2) style = 0;
    if (generators[style] == nil)
    {
        UIImpactFeedbackStyle styles[3] = { UIImpactFeedbackStyleLight, UIImpactFeedbackStyleMedium, UIImpactFeedbackStyleHeavy };
        generators[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:styles[style]];
        [generators[style] prepare];
    }
    [generators[style] impactOccurredWithIntensity:MAX(0.0f, MIN(1.0f, intensity))];
    [generators[style] prepare];
}
