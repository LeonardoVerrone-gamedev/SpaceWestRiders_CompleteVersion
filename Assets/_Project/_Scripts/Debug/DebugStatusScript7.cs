using UnityEngine;
using UnityEngine.Profiling;

public class DebugOverlay : MonoBehaviour
{
    [Header("Configurações de Exibição")]
    [SerializeField] private bool mostrarOverlay = true;
    [SerializeField] private Color corDoTexto = Color.green;
    [SerializeField] private int tamanhoDaFonte = 18;

    // Variáveis de cálculo do FPS
    private float tempoDecorrido = 0.0f;
    private int contadorFrames = 0;
    private float fpsAtual = 0.0f;
    private float tempoDeFrameMs = 0.0f;

    private void Update()
    {
        // Contagem de frames para média suave
        contadorFrames++;
        tempoDecorrido += Time.unscaledDeltaTime;

        if (tempoDecorrido >= 0.5f) // Atualiza as estatísticas a cada 0.5s
        {
            fpsAtual = contadorFrames / tempoDecorrido;
            tempoDeFrameMs = (tempoDecorrido / contadorFrames) * 1000.0f;
            
            contadorFrames = 0;
            tempoDecorrido = 0.0f;
        }
    }

    private void OnGUI()
    {
        if (!mostrarOverlay) return;

        // Configuração do estilo do texto
        GUIStyle estilo = new GUIStyle();
        estilo.fontSize = tamanhoDaFonte;
        estilo.fontStyle = FontStyle.Bold;
        estilo.normal.textColor = corDoTexto;

        // Informações da memória
        long memoriaUsadaMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);

        // Texto a ser exibido no topo da tela
        string statusText = $"FPS: {fpsAtual:F1} ({tempoDeFrameMs:F1} ms)\n" +
                           $"Memória Alocada: {memoriaUsadaMB} MB\n" +
                           $"Time Scale: {Time.timeScale}\n" +
                           $"Escala de Resolução: {Screen.width}x{Screen.height}";

        // Desenha uma caixa escura no fundo para facilitar a leitura
        GUI.Box(new Rect(10, 10, 320, 95), "");

        // Desenha o texto no topo esquerdo da tela
        GUI.Label(new Rect(18, 12, 300, 90), statusText, estilo);
    }

    private void OnDrawGizmos()
    {
        // Gizmo no Editor para indicar que o depurador está ativo no GameObject
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}