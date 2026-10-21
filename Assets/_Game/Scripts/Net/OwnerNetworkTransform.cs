using Unity.Netcode.Components;

namespace Vykhod
{
    /// <summary>NetworkTransform, которым управляет владелец (клиент), а не сервер.</summary>
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
