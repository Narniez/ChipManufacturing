using UnityEngine;

public interface IConveyorReciever 
{
    bool CanAccept(MaterialData material);
    bool TryAccept(ConveyorItem item);
}
