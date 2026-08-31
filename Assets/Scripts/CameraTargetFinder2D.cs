using Unity.Cinemachine;
using UnityEngine;

public class CameraTargetFinder2D : MonoBehaviour
{
    private void Start()
    {
        PlayerDoorInteractor2D player =
            FindFirstObjectByType<PlayerDoorInteractor2D>();

        if (player != null)
        {
            CinemachineCamera camera =
                GetComponent<CinemachineCamera>();

            if (camera != null)
            {
                camera.Follow = player.transform;
            }
        }
    }
}
