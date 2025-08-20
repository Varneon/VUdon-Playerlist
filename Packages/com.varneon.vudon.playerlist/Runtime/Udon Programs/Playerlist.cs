using JetBrains.Annotations;
using System;
using System.Diagnostics.CodeAnalysis;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using Varneon.VUdon.Editors;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace Varneon.VUdon.Playerlist
{
    /// <summary>
    /// Playerlist prefab for worlds to display information about the players currently in the instance
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [ExcludeFromPreset]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class Playerlist : UdonSharpBehaviour
    {
        #region Serialized Fields
        [SerializeField]
        [Range(1, 80)]
        [Tooltip("Maximum number of players which the world can hold.")]
        private int maxPlayerCount = 32;

        [FoldoutHeader("References")]
        [SerializeField, FieldNullWarning(true)]
        internal RectTransform windowRoot;

        [SerializeField, FieldNullWarning(true)]
        private GameObject playerListItem;

        [SerializeField, FieldNullWarning(true)]
        private GameObject roleListItem;

        [SerializeField, FieldNullWarning(true)]
        private GameObject roleListIconItem;

        [SerializeField, FieldNullWarning(true)]
        private RectTransform listRoot;

        [SerializeField, FieldNullWarning(true)]
        private TextMeshProUGUI textPlayerCount, textLocalPlaytime, textInstanceLifetime;
        #endregion

        #region Private Variables
        [UdonSynced]
        private long instanceStartTime;

        private long utcNow;

        private long localJoinTime = 0;

        private VRCPlayerApi localPlayer;

        private int localPlayerId;

        private VRCPlayerApi[] players;

        private int currentPlayerCount;

        private int totalPlayerCount;

        private int lastMasterId;

#pragma warning disable IDE0090 // Use 'new(...)'
        private readonly DataDictionary playerData = new DataDictionary();
#pragma warning restore IDE0090 // Use 'new(...)'
        #endregion

        private void Start()
        {
            localPlayerId = (localPlayer = Networking.LocalPlayer).playerId;

            UpdateUTCTime();

            localJoinTime = utcNow;

            lastMasterId = Networking.GetOwner(gameObject).playerId;

            if (localPlayer.isMaster)
            {
                instanceStartTime = utcNow;

                RequestSerialization();
            }

            _UpdateOnSecond();
        }

        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Prevent a method from being called over the network.")]
        public void _UpdateOnSecond()
        {
            UpdateUTCTime();

            textLocalPlaytime.text = GetFormattedDuration(localJoinTime);
            textInstanceLifetime.text = GetFormattedDuration(instanceStartTime);

            SendCustomEventDelayedSeconds(nameof(_UpdateOnSecond), 1f);
        }

        #region Utility Methods
        /// <summary>
        /// Updates
        /// <see cref="utcNow"/>
        /// </summary>
        private void UpdateUTCTime()
        {
            utcNow = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// Returns HH:MM:SS formatted string from ticks
        /// </summary>
        /// <param name="ticks"></param>
        /// <returns></returns>
        private string GetFormattedDuration(long ticks)
        {
            return TimeSpan.FromTicks(utcNow - ticks).ToString(@"hh\:mm\:ss");
        }

        /// <summary>
        /// Caches all players in the instance to
        /// <see cref="players"/>
        /// </summary>
        private void CachePlayers()
        {
            players = new VRCPlayerApi[currentPlayerCount = VRCPlayerApi.GetPlayerCount()];

            VRCPlayerApi.GetPlayers(players);
        }

        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Prevent a method from being called over the network.")]
        public void _UpdatePlayerInfoDelayed()
        {
            UpdatePlayerInfo(0);
        }

        private void UpdatePlayerInfo(int estimatedTotalPlayerCount)
        {
            CachePlayers();

            totalPlayerCount = (estimatedTotalPlayerCount > totalPlayerCount) ? estimatedTotalPlayerCount : totalPlayerCount;

            textPlayerCount.text = string.Join(" / ", currentPlayerCount, maxPlayerCount, totalPlayerCount);

            UpdateInstanceMaster();
        }

        private void UpdateInstanceMaster()
        {
            VRCPlayerApi master = Networking.GetOwner(gameObject);

            int playerId = master.playerId;

            if (lastMasterId != playerId && TryGetPlayerItem(playerId, out RectTransform item))
            {
                item.GetComponentInChildren<TextMeshProUGUI>(true).text = GetFormattedIdText(playerId, true, master.isLocal);

                lastMasterId = playerId;
            }
        }

        private bool TryGetPlayerItem(int id, out RectTransform item)
        {
            if (playerData.TryGetValue(id, TokenType.Reference, out DataToken itemToken))
            {
                item = (RectTransform)itemToken.Reference;

                return true;
            }

            item = null;

            return false;
        }

        private string GetFormattedIdText(int id, bool isMaster, bool isLocal)
        {
            return string.Concat("<color=#80C4FF><size=10>", isMaster ? "MASTER" : " ", "</size></color>\n", id, "\n<color=#80C4FF><size=10>", isLocal ? "YOU" : " ", "</size></color>");
        }

        private bool TryValidatePlayer(VRCPlayerApi player, out int playerId)
        {
            if (Utilities.IsValid(player)) { playerId = player.playerId; return true; }

            playerId = -1;

            return false;
        }

        private void TryAddPlayer(int playerId)
        {
            if (playerData.ContainsKey(playerId)) { return; }

            AddPlayer(VRCPlayerApi.GetPlayerById(playerId));
        }

        private void AddPlayer(VRCPlayerApi player)
        {
            if (!TryValidatePlayer(player, out int playerId)) { return; }

            if (playerData.ContainsKey(player.playerId)) { return; }

            GameObject newPlayerListItem = Instantiate(playerListItem, listRoot, false);

            TextMeshProUGUI[] texts = newPlayerListItem.GetComponentsInChildren<TextMeshProUGUI>(true);

            bool isLocalPlayer = player.isLocal;

            texts[0].text = GetFormattedIdText(playerId, player.isMaster, player.isLocal);
            texts[1].text = player.displayName;

            if (Networking.LocalPlayer.playerId <= player.playerId)
            {
                texts[3].text = DateTime.UtcNow.ToLocalTime().ToString("ddd, h:mm tt");
            }

            if (isLocalPlayer) { GetListItemHighlight(newPlayerListItem.transform).gameObject.SetActive(true); }

            playerData.Add(player.playerId, (RectTransform)newPlayerListItem.transform);

            LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);

            UpdatePlayerInfo(playerId);
        }

        private void RemovePlayer(VRCPlayerApi player)
        {
            if(TryValidatePlayer(player, out int playerId) && TryGetPlayerItem(player.playerId, out RectTransform item))
            {
                playerData.Remove(playerId);

                Destroy(item.gameObject);
            }

            SendCustomEventDelayedFrames(nameof(_UpdatePlayerInfoDelayed), 0);
        }

        public override void OnPlayerJoined(VRCPlayerApi player) { AddPlayer(player); }

        public override void OnPlayerLeft(VRCPlayerApi player) { RemovePlayer(player); }
        #endregion

        #region Hierarchy Accessors
        private Transform GetListItemHighlight(Transform listItem) { return listItem.GetChild(1).GetChild(0); }

        private Transform GetListItemRoleContainer(Transform listItem) { return listItem.GetChild(1).GetChild(4); }

        private TextMeshProUGUI GetListItemStatusText(Transform listItem) { return listItem.GetChild(1).GetChild(2).GetComponent<TextMeshProUGUI>(); }
        #endregion

        #region Public API
        /// <summary>
        /// Try to add a role to a player
        /// </summary>
        /// <param name="playerId">The ID of the player</param>
        /// <param name="name">Name of the role</param>
        /// <param name="color">Color of the role</param>
        /// <returns>Does the player exist</returns>
        [PublicAPI]
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Prevent a method from being called over the network.")]
        public bool _TryAddRoleToPlayer(int playerId, string name, Color color)
        {
            TryAddPlayer(playerId);

            if(!TryGetPlayerItem(playerId, out RectTransform playerItem)) { Debug.LogError("Couldn't get player item!"); return false; }

            RectTransform roleContainer = (RectTransform)GetListItemRoleContainer(playerItem);

            GameObject newRoleItem = Instantiate(roleListItem, roleContainer, false);

            newRoleItem.GetComponent<Image>().color = color;

            newRoleItem.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = name;

            LayoutRebuilder.ForceRebuildLayoutImmediate(roleContainer);

            return true;
        }

        /// <summary>
        /// Try to add a role to a player
        /// </summary>
        /// <param name="playerId">The ID of the player</param>
        /// <param name="icon">Icon of the role</param>
        /// <returns>Does the player exist</returns>
        [PublicAPI]
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Prevent a method from being called over the network.")]
        public bool _TryAddRoleToPlayer(int playerId, Sprite icon)
        {
            TryAddPlayer(playerId);

            if (!TryGetPlayerItem(playerId, out RectTransform playerItem)) { Debug.LogError("Couldn't get player item!"); return false; }

            RectTransform roleContainer = (RectTransform)GetListItemRoleContainer(playerItem);

            GameObject newRoleItem = Instantiate(roleListIconItem, roleContainer, false);

            newRoleItem.GetComponent<Image>().sprite = icon;

            LayoutRebuilder.ForceRebuildLayoutImmediate(roleContainer);

            return true;
        }

        /// <summary>
        /// Try set the visible status of a player
        /// </summary>
        /// <param name="playerId">The ID of the player</param>
        /// <param name="status">New status of the player</param>
        /// <returns>Does the player exist</returns>
        [PublicAPI]
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Prevent a method from being called over the network.")]
        public bool _TrySetPlayerStatus(int playerId, string status)
        {
            TryAddPlayer(playerId);

            if (!TryGetPlayerItem(playerId, out RectTransform playerItem)) { Debug.LogError("Couldn't get player item!"); return false; }

            GetListItemStatusText(playerItem).text = status;

            return true;
        }
        #endregion
    }
}
