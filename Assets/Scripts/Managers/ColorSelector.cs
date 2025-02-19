using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class ColorSelector : MonoBehaviour
{
    [SerializeField] private Button[] colorButtons;
    [SerializeField] private GameObject colorSelectionPanel;

    private static Color selectedColor = Color.green;
    private static bool hasSelectedColor = false;

    public static Color[] availableColors = new Color[]
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.magenta
    };

    void Start()
    {
        InitializeColorButtons();
    }

    void InitializeColorButtons()
    {
        for (int i = 0; i < colorButtons.Length && i < availableColors.Length; i++)
        {
            Button button = colorButtons[i];
            Color buttonColor = availableColors[i];

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = buttonColor;
            }

            int index = i;
            button.onClick.AddListener(() => 
            {
                OnColorSelected(availableColors[index]);
                Debug.Log($"Button clicked - Color selected: {availableColors[index]}");
            });
        }
    }

    void OnColorSelected(Color color)
    {
        selectedColor = color;
        hasSelectedColor = true;
        Debug.Log($"Color selection saved: {selectedColor}");

        foreach (Button button in colorButtons)
        {
            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                button.transform.localScale = 
                    buttonImage.color == selectedColor ? 
                    new Vector3(1.2f, 1.2f, 1.2f) : Vector3.one;
            }
        }
    }

    public static Color GetSelectedColor()
    {
        if (!hasSelectedColor)
        {
            Debug.LogWarning("No color selected, using default green");
            return Color.green;
        }
        Debug.Log($"Returning selected color: {selectedColor}");
        return selectedColor;
    }

    public void OnHostButtonClicked()
    {
        if (!hasSelectedColor)
        {
            Debug.LogWarning("No color selected before hosting");
            OnColorSelected(Color.green);
        }
        Debug.Log($"Starting host with color: {selectedColor}");
        NetworkManager.Singleton.StartHost();
        HideColorSelection();
    }

    public void OnJoinButtonClicked()
    {
        if (!hasSelectedColor)
        {
            Debug.LogWarning("No color selected before joining");
            OnColorSelected(Color.green);
        }
        Debug.Log($"Starting client with color: {selectedColor}");
        NetworkManager.Singleton.StartClient();
        HideColorSelection();
    }

    private void HideColorSelection()
    {
        if (colorSelectionPanel != null)
        {
            colorSelectionPanel.SetActive(false);
        }
    }
}