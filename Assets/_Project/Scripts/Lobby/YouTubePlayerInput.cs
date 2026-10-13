using UnityEngine;
using UnityEngine.EventSystems;
public sealed class YouTubePlayerInput : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IDragHandler
{
 public LobbyTelevision Television {get;set;}
 private void Send(PointerEventData e,bool down,bool up)
 {
  if(Television==null||e.button!=PointerEventData.InputButton.Left)return;
  var rect=(RectTransform)transform;if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out var point))return;
  Television.PlayerPointer(new Vector2(Mathf.Clamp01((point.x-rect.rect.xMin)/rect.rect.width)*640,(1-Mathf.Clamp01((point.y-rect.rect.yMin)/rect.rect.height))*360),down,up);
 }
 public void OnPointerDown(PointerEventData e)=>Send(e,true,false);
 public void OnPointerUp(PointerEventData e)=>Send(e,false,true);
 public void OnDrag(PointerEventData e)=>Send(e,false,false);
}
