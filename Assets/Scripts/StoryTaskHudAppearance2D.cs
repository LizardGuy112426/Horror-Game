using UnityEngine;
using UnityEngine.UI;

/// <summary>Inspector-facing appearance slots for the persistent story task HUD.</summary>
[ExecuteAlways]
public sealed class StoryTaskHudAppearance2D : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image taskIconImage;
    [SerializeField] private Text taskLabel;

    [Header("Drop Your Own UI Here")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite taskIconSprite;
    [SerializeField] private Font taskFont;

    [Header("Appearance")]
    [SerializeField] private Color backgroundColor = new(0.035f, 0.03f, 0.05f, 0.82f);
    [SerializeField] private Color iconColor = Color.white;
    [SerializeField, Min(1)] private int fontSize = 32;

    public void Configure(
        Image background,
        Image icon,
        Text label,
        Sprite defaultBackground,
        Sprite defaultIcon,
        Font defaultFont)
    {
        backgroundImage = background;
        taskIconImage = icon;
        taskLabel = label;
        backgroundSprite = defaultBackground;
        taskIconSprite = defaultIcon;
        taskFont = defaultFont;
        ApplyAppearance();
    }

    [ContextMenu("Apply Task HUD Appearance")]
    public void ApplyAppearance()
    {
        if (backgroundImage != null)
        {
            backgroundImage.sprite = backgroundSprite;
            backgroundImage.color = backgroundColor;
        }

        if (taskIconImage != null)
        {
            taskIconImage.sprite = taskIconSprite;
            taskIconImage.color = iconColor;
            taskIconImage.preserveAspect = true;
        }

        if (taskLabel != null)
        {
            if (taskFont != null)
                taskLabel.font = taskFont;
            taskLabel.fontSize = Mathf.Max(1, fontSize);
        }
    }

    private void OnValidate()
    {
        fontSize = Mathf.Max(1, fontSize);
        ApplyAppearance();
    }
}
