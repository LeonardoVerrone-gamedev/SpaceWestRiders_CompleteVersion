using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionAnimationManager : MonoBehaviour
{
    public static SceneTransitionAnimationManager Instance { get; private set; }

    [Header("Componentes")]
    public Animator anim;
    [SerializeField] private CanvasGroup canvasGroup; // Opcional: para garantir que cliques sejam bloqueados

    private bool estaCarregando = false;

    private void Awake()
    {
        // Configura o Singleton unificado
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Garante que o Canvas de transição sobreviva ao LoadScene

        // Garante que o CanvasGroup comece liberado caso você use um
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        // Se inscreve no evento nativo da Unity para limpar estados e rodar o Out automaticamente
        SceneManager.sceneLoaded += AoCarregarNovaCena;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= AoCarregarNovaCena;
    }

    /// <summary>
    /// Método principal para você chamar de qualquer lugar do jogo via:
    /// SceneTransitionAnimationManager.Instance.CarregarCena("NomeDaCena");
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (estaCarregando) return;

        StartCoroutine(FluxoDeTransicao(sceneName));
    }

    private IEnumerator FluxoDeTransicao(string sceneName)
    {

        Debug.Log("Iniciou coroutine IN");
        estaCarregando = true;

        // 1. Bloqueia cliques na tela para o jogador não apertar botões repetidos no loading
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        // 2. Dispara a animação de entrada (O Nitro cobrindo a tela)
        FadeIn();

        // 3. Espera o tempo da animação terminar. 
        // Usamos WaitForSecondsRealtime para funcionar mesmo se o jogo estiver pausado (Time.timeScale = 0)
        yield return new WaitForSecondsRealtime(ObterTempoDaAnimacaoActive());

        // 4. Carrega a nova cena de forma assíncrona em background
        AsyncOperation operacaoCarregamento = SceneManager.LoadSceneAsync(sceneName);

        // Segura o código aqui até que a Unity termine de ler os arquivos da nova cena
        while (!operacaoCarregamento.isDone)
        {
            yield return null;
        }
    }

    private void AoCarregarNovaCena(Scene cena, LoadSceneMode modo)
    {
        // Só executa o FadeOut se o gatilho veio do nosso fluxo de carregamento
        if (estaCarregando)
        {
            StartCoroutine(FluxoDeSaida());
        }
    }

    private IEnumerator FluxoDeSaida()
    {
        // 5. Dispara a animação de saída (Revelando a pista/nova tela)
        FadeOut();

        // 6. Espera a animação de saída concluir
        yield return new WaitForSecondsRealtime(ObterTempoDaAnimacaoActive());

        // 7. Libera os controles e o estado do gerenciador
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        estaCarregando = false;
    }

    public void FadeIn()
    {
        if (anim != null)
        {
            anim.Play("Transition_In");
        }
    }

    public void FadeOut()
    {
        if (anim != null)
        {
            anim.Play("Transition_Out");
        }
    }

    /// <summary>
    /// Função auxiliar que lê o tempo exato do clipe de animação atual do seu Animator, 
    /// evitando que você precise engessar valores flutuantes de "tempo de espera" no código.
    /// </summary>
    private float ObterTempoDaAnimacaoActive()
    {
        return 1.25f;
    }
}