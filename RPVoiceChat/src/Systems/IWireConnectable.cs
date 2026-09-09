using System.Collections.Generic;
using RPVoiceChat.GameContent.Systems;
using Vintagestory.API.MathTools;

public interface IWireConnectable
{
    BlockPos Position { get; }
    long NetworkUID { get; set; }

    IReadOnlyList<WireConnection> GetConnections();

    void AddConnection(WireConnection connection);
    bool HasConnection(WireConnection connection);

    void MarkForUpdate();
}

