using UnityEngine;

public interface IConveyorReciever 
{
    bool CanAccept(MaterialType material);
    bool TryAccept(ConveyorItem item);
}
