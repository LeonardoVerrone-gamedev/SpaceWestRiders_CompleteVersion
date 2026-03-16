using UnityEngine;
using UnityEngine.UI;

public class OpponentUIIndicator : MonoBehaviour
{
    public Image icon;
    public RacerStatus targetRacer;
    
    public void UpdateUI(float screenX, float scale, bool visible)
    {
        icon.enabled = visible;
        if (!visible) return;

        // Posiciona no fundo da tela (ajuste o Y conforme seu layout)
        RectTransform rt = icon.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);

        // screenX varia de -1 (esquerda) a 1 (direita)
        float horizontalPadding = 800f; // Quão longe ele vai para os lados
        rt.anchoredPosition = new Vector3(screenX * horizontalPadding, 50f, 0f);
        
        rt.localScale = Vector3.one * scale;
    }
}