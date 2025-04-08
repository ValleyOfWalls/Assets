using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public float horizontal;
    public float vertical;
    public NetworkButtons buttons;
    
    // Button mapping:
    // 0 - Jump (kept for backward compatibility)
    // 1 - Ready toggle (use IsSet to check if pressed)
    // 2 - Additional action
}