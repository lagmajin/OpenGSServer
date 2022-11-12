﻿using System;
using OpenGSCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;


namespace OpenGSServer
{

    public interface IMatchRoom
    {

        string Name { get; set; }


    }
    public class MatchRoom : AbstractGameRoom, IMatchRoom, IDisposable
    {


        AbstractMatchRule? rule;


        private MatchSettings Setting { get; set; }


        private GameScene scene = new();

        //[CanBeNull]
        public WaitRoom? WaitRoomLink { get; private set; } = null;
        public string Name { get; set; }



        public bool MatchEnd { get; }

        public int PlayerCount { get; }

        public int Capacity { get; }


        public bool Playing { get; private set; } = false;
        public bool Finished { get; } = false;



        public MatchRoom(int roomNumber, in string roomName, in string roomOwnerId, in AbstractMatchRule rule) : base(roomNumber, roomOwnerId)
        {



            //ConsoleWrite.WriteMessage("Match Room Name:" + "" + "Capacity:" + "Room ID:" + id.ToString() + "GameMode:", ConsoleColor.Yellow);
        }


        public bool IsOwner(in string id)
        {
            return false;
        }

        public bool ChangeOwner()
        {
            if (Players.Count <= 1)
            {
                return false;
            }



            return false;
        }

        public bool ChangeOwnerRandom()
        {
            if (Players.Count < 1)
            {
                return false;
            }

            //var currentOwnerID=owner



            return false;
        }

#nullable enable
        public string? RoomOwnerName()
        {
            if (Players.Count == 0)
            {
                return null;
            }
            else
            {

            }





            return "";
        }

        public PlayerInfo? RoomOwnerInfo()
        {

            return null;
        }

        public List<string> RoomMembersNameList()
        {
            var result = new List<string>();

            return result;
        }

        public List<PlayerInfo>? RoomMemberList()
        {



            return Players;
        }

        public bool IsMember(in string id)
        {
            if (Players.Count == 0)
            {
                return false;
            }
            else
            {

            }

            return false;
        }

        public GameScene Scene { get => scene; set => scene = value; }

        /*
        public long ElapsedTime()
        {
            return sw.ElapsedMilliseconds;
        }
        */

        public void AddNewPlayer(PlayerInfo info)
        {

            if (Playing)
            {

            }
            else
            {

            }



        }

        public override void GameUpdate()
        {





        }

        public void Start()
        {
            //timer.Start();
            //sw.Start();

            if (Setting.TimeLimit)
            {
                //setting.MatchTime;

            }


            Playing = true;
        }

        public void Finish()
        {



            Playing = false;

        }





        public JObject ToJson()
        {
            var json = new JObject();

            json["RoomName"] = "";
            json["RoomID"] = Id.ToString();
            json["MaxCapacity"] = 2;
            json["PlayerCount"] = PlayerCount;


            return json;

        }

        public void Dispose()
        {



        }
    }
}
