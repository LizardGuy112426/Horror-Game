using UnityEngine;

/// <summary>Creates the shared World Space E image used by Nightmare task interactions.</summary>
public sealed class WorldEPrompt2D : MonoBehaviour
{
    private const string ResourceName = "WorldEPrompt2D";

    public static GameObject GetOrCreate(Transform parent, Vector3 localPosition)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find("E Prompt");
        GameObject prompt = existing != null ? existing.gameObject : null;
        if (prompt != null && prompt.GetComponent<WorldEPrompt2D>() == null)
        {
            // Old task objects could contain a TextMesh-style prompt. Keep it hidden
            // and replace it so every Nightmare task uses the shared E image.
            prompt.name = "Legacy E Prompt";
            prompt.SetActive(false);
            prompt = null;
        }

        if (prompt == null)
        {
            GameObject prefab = Resources.Load<GameObject>(ResourceName);
            if (prefab == null)
            {
                Debug.LogWarning(
                    "WorldEPrompt2D prefab is missing from Assets/Resources. "
                    + "Task interactions will remain usable but will not show an E prompt.");
                return null;
            }

            prompt = Instantiate(prefab, parent, false);
            prompt.name = "E Prompt";
        }

        prompt.transform.localPosition = localPosition;
        prompt.SetActive(false);
        return prompt;
    }
}
