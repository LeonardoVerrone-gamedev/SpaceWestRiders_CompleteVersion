using UnityEngine;
using UnityEngine.EventSystems;

public class CircuitButtonData : MonoBehaviour, 
    IPointerEnterHandler, 
    ISelectHandler
{
    public string circuitID;

    public static CircuitButtonData CurrentHovered; 

    private CircuitSelection menuPrincipal;

    void OnEnable()
    {
        // Força buscar o menu ativo na cena toda vez que o botão for ligado
        BuscarMenuAtivo();
    }

    void OnDisable()
    {
        // Limpa referências estáticas para não vazar memória entre cenas
        if (CurrentHovered == this)
        {
            CurrentHovered = null;
        }
    }

    private void BuscarMenuAtivo()
    {
        menuPrincipal = FindFirstObjectByType<CircuitSelection>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CurrentHovered = this;
        NotificarMudanca();
    }

    public void OnSelect(BaseEventData eventData)
    {
        CurrentHovered = this;
        NotificarMudanca();
    }

    private void NotificarMudanca()
    {
        // Se por algum motivo mudou de cena ou perdeu o ponteiro, busca novamente o painel funcional da hierarquia
        if (menuPrincipal == null) 
        {
            BuscarMenuAtivo();
        }

        if (menuPrincipal != null)
        {
            menuPrincipal.NotificarNovoBotaoFocado(this);
        }
    }
}