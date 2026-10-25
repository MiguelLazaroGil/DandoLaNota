using System;
using Unity.Collections;
using Unity.Netcode;

public enum PlayerRole : byte
{
    Ghost = 0,
    Director = 1
}

public struct PlayerLobbyData : INetworkSerializable, IEquatable<PlayerLobbyData>
{
    public ulong ClientId;
    public FixedString32Bytes PlayerName;
    public ulong PingMs;
    public PlayerRole Role;

    public PlayerLobbyData(ulong clientId, string playerName, ulong pingMs = 0, PlayerRole role = PlayerRole.Ghost)
    {
        ClientId = clientId;
        PlayerName = playerName;
        PingMs = pingMs;
        Role = role;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref PingMs);

        // Convertir enum a byte para asegurar la transmisión correcta por la red
        byte roleByte = (byte)Role;
        serializer.SerializeValue(ref roleByte);
        Role = (PlayerRole)roleByte;
    }

    public bool Equals(PlayerLobbyData other)
    {
        return ClientId == other.ClientId &&
               PlayerName.Equals(other.PlayerName) &&
               PingMs == other.PingMs &&
               Role == other.Role;
    }
}