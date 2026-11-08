using UnityEngine;

namespace AstroFlowJam
{
    public sealed partial class LevelController : MonoBehaviour
    {
        LevelCamera levelCamera;

        void FitCamera()
        {
            if (levelCamera == null || !levelCamera.Fit(cameraTilt, cameraFov)) return;
            // Pikseller yörüngenin (kapının) ortasında görünsün: eğik kamerada perspektif kayması telafi edilir.
            if (field) field.AlignToView(worldCamera, track.transform.TransformPoint(new Vector3(0f, 0f, DoorDepth)));
        }
    }
}
