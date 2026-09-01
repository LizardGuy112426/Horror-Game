using Unity.Cinemachine;
using UnityEngine;

public class CameraTargetFinder2D : MonoBehaviour
{
    private void Start()
    {
        BindToPersistentPlayer();
    }

    public void BindToPersistentPlayer()
    {
        PlayerDoorInteractor2D player = MCControllers.Instance != null
            ? MCControllers.Instance.GetComponent<PlayerDoorInteractor2D>()
            : FindAnyObjectByType<PlayerDoorInteractor2D>();

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
