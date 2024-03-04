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
        private GameObject playerListItem;

        [SerializeField, FieldNullWarning(true)]
        private GameObject roleListItem;

        [SerializeField, FieldNullWarning(true)]
        private RectTransform listRoot;

        [SerializeField, FieldNullWarning(true)]
        private TextMeshProUGUI textPlayerCount, textLocalPlaytime, textInstanceLifetime;
        #endregion

        #region Private Variables
        [UdonSynced]
        private long instanceStartTime;

        // Experimental instance data for keeping track of all players
        //private DataDictionary instanceData = new DataDictionary();

        private long utcNow;

        private long localJoinTime = 0;

        private VRCPlayerApi localPlayer;

        private int localPlayerId;

        private VRCPlayerApi[] players;

        private int currentPlayerCount;

        private int totalPlayerCount;

        private int lastMasterId;

        private readonly DataDictionary playerData = new DataDictionary();
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

            if (lastMasterId != playerId && TryGetPlayerItem(playerId, out Transform item))
            {
                item.GetComponentInChildren<TextMeshProUGUI>(true).text = GetFormattedIdText(playerId, true, master.isLocal);

                lastMasterId = playerId;
            }
        }

        private bool TryGetPlayerItem(int id, out Transform item)
        {
            if (playerData.TryGetValue(id.ToString(), TokenType.Reference, out DataToken itemToken))
            {
                item = (Transform)itemToken.Reference;

                return true;
            }

            item = null;

            return false;
        }

        private string GetFormattedIdText(int id, bool isMaster, bool isLocal)
        {
            return string.Concat("<color=#80C4FF><size=10>", isMaster ? "MASTER" : " ", "</size></color>\n", id, "\n<color=#80C4FF><size=10>", isLocal ? "YOU" : " ", "</size></color>");
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            GameObject newPlayerListItem = Instantiate(playerListItem, listRoot, false);

            int playerId = player.playerId;

            TextMeshProUGUI[] texts = newPlayerListItem.GetComponentsInChildren<TextMeshProUGUI>(true);

            texts[0].text = GetFormattedIdText(playerId, player.isMaster, player.isLocal);
            texts[1].text = player.displayName;

            if(Networking.LocalPlayer.playerId <= player.playerId)
            {
                texts[3].text = DateTime.UtcNow.ToLocalTime().ToString("ddd, h:mm tt");
            }

            playerData.Add(player.playerId.ToString(), newPlayerListItem.transform);

            LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);

//            if (Networking.IsOwner(gameObject))
//            {
//                DataDictionary container = new DataDictionary();

//#pragma warning disable IDE0028 // UdonSharp does not support initializer lists yet
//                container.Add("joined", DateTime.UtcNow.ToFileTime());
//#pragma warning restore IDE0028

//                instanceData.Add(player.playerId.ToString(), container);

//                Debug.Log(instanceData.ToString());

//                if (VRCJson.TrySerializeToJson(instanceData, JsonExportType.Beautify, out DataToken instanceDataOutput))
//                {
//                    Debug.Log(instanceDataOutput);
//                }
//            }

            UpdatePlayerInfo(playerId);
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && TryGetPlayerItem(player.playerId, out Transform item))
            {
                playerData.Remove(player.playerId);

                Destroy(item.gameObject);
            }

            SendCustomEventDelayedFrames(nameof(_UpdatePlayerInfoDelayed), 0);
        }
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
            if(!TryGetPlayerItem(playerId, out Transform playerItem)) { return false; }

            RectTransform roleContainer = (RectTransform)playerItem.GetChild(1).GetChild(3);

            GameObject newRoleItem = Instantiate(roleListItem, roleContainer, false);

            newRoleItem.GetComponent<Image>().color = color;

            newRoleItem.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = name;

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
            if (!TryGetPlayerItem(playerId, out Transform playerItem)) { return false; }

            playerItem.GetChild(1).GetChild(1).GetComponent<TextMeshProUGUI>().text = status;

            return true;
        }
        #endregion
    }
}
