using UnityEngine;

public interface IEnemy
{
    ColorComponent GetColorComponent();
    GameObject GetGameObject();
    void OnColorInteraction(ColorInteractionEvent interaction);
}
