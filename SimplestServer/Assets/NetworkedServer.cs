using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;



    #region MyGlobalVariables
   
    // -list of game rooms
    LinkedList<GameRoom> game_rooms;

    // -list of exsting players
    LinkedList<PlayerAccount> player_accounts;

    // -list of players In Public Chat room
    LinkedList<PlayerAccount> active_players_connected_global_chat;

    //LinkedList<string> PlayerRecordManagerFile;

    // 
    //int player_waiting_for_match_with_id = -1;

    //- use in Queue system bed
    //- use Global Temporary A player
    PlayerAccount player_waiting_for_match = new PlayerAccount("TempPlayer", -1);
    #endregion 


    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);



        game_rooms = new LinkedList<GameRoom>();

        active_players_connected_global_chat = new LinkedList<PlayerAccount>();

        player_accounts = new LinkedList<PlayerAccount>();

       // PlayerRecordManagerFile = new LinkedList<string>();

        // read in player accounts from wherever
        LoadPlayerManagementFile();
    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);

                // ClientList.AddLast(new Clinet(recConnectionID));
                
                break;
            case NetworkEventType.DataEvent:

                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);

                ProcessRecievedMsg(msg, recConnectionID);

                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);

                //DisconnectFromGame(recConnectionID);

                // PlayerDisconnectFromQueueGame(recConnectionID);// if a player disconnect durring queue.

                //PlayerDisconnectionFromPublicChat(recConnectionID);

                //SendClearListofPlayersToClient();


                //DisconnectFromGame(recConnectionID);

                

                GeneralDisconnected(recConnectionID);

                

                break;
        }

    }
  
    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }
    
    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');

        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToServerSignifiers.CreateAcount)
        {
            //CreatedAccount(csv[1], csv[2], id);

            Debug.Log("create an Account");
            // check if player  account name already exists,

            // local Variables
            bool is_name_in_use = false;
            string user_name = csv[1];
            string password = csv[2];

            foreach (PlayerAccount pa in player_accounts)
            {
                if (pa.name_ == user_name)
                {
                    is_name_in_use = true;
                    break;
                }
            }

            if (is_name_in_use)
            {
                SendMessageToClient(ServerToClientSignifiers.CreateAcountFailed + ",8", id);
                Debug.LogWarning("This Account already exist");
            }
            else
            {
                // Create  new account, add to list

                PlayerAccount newPlayAccount = new PlayerAccount(user_name, password);
                player_accounts.AddLast(newPlayAccount);
                SendMessageToClient(ServerToClientSignifiers.CreateAcountComplete + ",8", id);

                // save list to HD
                SavePlayerManagementFile();
                Debug.LogWarning("This Account has created and add");
            }
            // If not, 
            // send to success/ failure
        }
        else if (signifier == ClientToServerSignifiers.Login)
        {
            // Login(csv[1], csv[2], id);

            Debug.Log("Login to an account");
            // check if player  account name already exists,

            bool is_name_in_use = false;
            bool is_passward_in_use = false;
            string user_name = csv[1];
            string password = csv[2];

            foreach (PlayerAccount pa in player_accounts)
            {
                if (pa.name_ == user_name)
                {
                    is_name_in_use = true;
                    break;
                }
            }

            if (is_name_in_use)
            {
                foreach (PlayerAccount pa in player_accounts)
                {
                    if (pa.name_ == user_name && pa.password_ == password)
                    {
                        is_passward_in_use = true;
                        break;
                    }
                }

                if (is_passward_in_use)
                {
                    Debug.LogWarning("Password was right. You are in your Account");
                    SendMessageToClient(ServerToClientSignifiers.LoginComplete + "," + user_name, id);

                }
                else
                {
                    SendMessageToClient(ServerToClientSignifiers.LoginFailedPassword + ",8", id);
                    Debug.LogWarning("Password was wrong");
                }
            }
            else
            {
                Debug.LogWarning("This Account doesn't exist");
                SendMessageToClient(ServerToClientSignifiers.LoginFailedAccount + ", 8", id);
            }

            // send to success/ failure
        }
        else if (signifier == ClientToServerSignifiers.SendChatMsg)
        {
            string Msg = csv[1];

            NotifyPublicChatRoomUsersWithAMsg(Msg);
        }
        else if (signifier == ClientToServerSignifiers.EnterTheChatRoom)
        {
            //AddPlayerToTheChat(csv[1], id);

            string user_name = csv[1];

            // add to list player in public chat room
            active_players_connected_global_chat.AddLast(new PlayerAccount(user_name, id));
           

            // join chat msg and send it all in public chat
            string join_chat_msg = "< " + user_name + " > Have just join the chat.";
            NotifyPublicChatRoomUsersWithAMsg(join_chat_msg);

            // Update list of player for private messaging in Public chat room.
            // so you can see who in the chat.
            NotifyPublicChatRoomClientsOfChanges();
        }
        else if (signifier == ClientToServerSignifiers.Logout)
        {
            //LogOutPlayer(id);

            PlayerAccount temp_player_account = new PlayerAccount();
            bool is_player_in_chat = false;

            foreach (PlayerAccount pa in active_players_connected_global_chat)
            {
                if (id == pa.connection_id_)
                {
                    temp_player_account = pa;
                    is_player_in_chat = true;
                    break;
                }
            }

            active_players_connected_global_chat.Remove(temp_player_account);
            Debug.LogWarning("TempPlayerAccount : " + temp_player_account.connection_id_.ToString());

            /// 
            if (temp_player_account.name_ != "" && temp_player_account.name_ != null && is_player_in_chat)
            {
                string LogOutMsgOfChat = "< " + temp_player_account.name_ + " > Has Logout.";
                NotifyPublicChatRoomUsersWithAMsg(LogOutMsgOfChat);

                LogOutMsgOfChat = ServerToClientSignifiers.LogOutComplete + ",8";

                //SendClearListofPlayersToClient();

                NotifyPublicChatRoomClientsOfChanges();

                SendMessageToClient(LogOutMsgOfChat, id);
                return;

            }

        }
        else if (signifier == ClientToServerSignifiers.SendChatPrivateMsg)
        {
            // SendIngPrivateMessageInChat(csv[1], csv[2], id);

            string global_chat_pm = csv[1];
            string user_name = csv[2];

            PlayerAccount specifier_player_in_chat = new PlayerAccount();
            bool is_player_real = false;

            foreach (PlayerAccount pa in active_players_connected_global_chat)
            {
                if (pa.name_ == user_name)
                {
                    specifier_player_in_chat = pa;
                    is_player_real = true;
                    break;
                }
            }
            if (is_player_real)
            {
                if (specifier_player_in_chat.connection_id_ != id)
                {
                    SendMessageToClient(ServerToClientSignifiers.ReceivePrivateChatMsg + "," + global_chat_pm, id);
                    SendMessageToClient(ServerToClientSignifiers.ReceivePrivateChatMsg + "," + global_chat_pm, specifier_player_in_chat.connection_id_);
                }
                else
                {
                    SendMessageToClient(ServerToClientSignifiers.ReceivePrivateChatMsg + "," + global_chat_pm, id);
                }
            }


        }
        else if (signifier == ClientToServerSignifiers.JoinQueueForGameRoom)
        {
            Debug.Log(" We need to get this player in a waiting Queue!!!");

            if (player_waiting_for_match.connection_id_ == -1)
            {
                player_waiting_for_match.connection_id_ = id;
                player_waiting_for_match.name_ = csv[1];
            }
            else
            {
                // so what if the player with their id being stored in PlayWaitingForMatchWithID has left???

                if (player_waiting_for_match.connection_id_ != -1) // check if first player in queue disconnect
                {
                    GameRoom gr = new GameRoom(player_waiting_for_match, new PlayerAccount(csv[1], id));
                    game_rooms.AddLast(gr);


                    SendMessageToClient(ServerToClientSignifiers.ReceiveOpponentName + "," + gr.player_one_.name_, gr.player_two_.connection_id_);
                    SendMessageToClient(ServerToClientSignifiers.ReceiveOpponentName + "," + gr.player_two_.name_, gr.player_one_.connection_id_);

                    SendMessageToClient(ServerToClientSignifiers.GameStart + ", 2", gr.player_two_.connection_id_);
                    SendMessageToClient(ServerToClientSignifiers.GameStart + ", 1", gr.player_one_.connection_id_);

                    //PlayerWaitingForMatchWithID = -1;
                    player_waiting_for_match = new PlayerAccount("TempPlayer", -1);
                }
            }
        }
        else if (signifier == ClientToServerSignifiers.TicTacToesSomethingSomthing)
        {
            GameRoom gr = GetGameRoomClientId(id);
            if (gr != null)
            {
                if (gr.player_one_.connection_id_ == id)
                {

                    SendMessageToClient(ServerToClientSignifiers.OpponentPlayed + "," + csv[1], gr.player_two_.connection_id_);

                    SendMessageToClient(ServerToClientSignifiers.WaitForOppentMoved + ",0", gr.player_one_.connection_id_);// make the play wait


                    Debug.LogWarning("-> Player name : " + gr.player_one_.name_ + " Position: " + csv[1]);
                    // storing match data here:
                    gr.match_data_.AddLast(new MatchData( gr.player_one_.name_,int.Parse(csv[1]), 1));

                }
                else
                {
                    SendMessageToClient(ServerToClientSignifiers.OpponentPlayed + "," + csv[1], gr.player_one_.connection_id_);

                    SendMessageToClient(ServerToClientSignifiers.WaitForOppentMoved + ",0", gr.player_two_.connection_id_);// make the play wait


                    Debug.LogWarning("-> Player name : " + gr.player_two_.name_ + " Position: " + csv[1]);
                    // storing match data here:
                    gr.match_data_.AddLast(new MatchData(gr.player_two_.name_ , int.Parse(csv[1]), 2 ));
                }
                //Bug:we never clean up our GameRooms, even One players leaves
                // we need to 
            }

        }
        else if (signifier == ClientToServerSignifiers.ReMatchOfTicTacToe)
        {
            GameRoom gr = GetGameRoomClientId(id);


            if (gr.player_one_.connection_id_ == id)
            {
              
                gr.agree_to_rematch_[0] = 1;
            }
            else
            {
                
                gr.agree_to_rematch_[1] = 1;
            }

            if (gr.agree_to_rematch_[0] == 1 && gr.agree_to_rematch_[1] == 1)
            {
                SendMessageToClient(ServerToClientSignifiers.ReMatchOfTicTacToeComplete + ",2", gr.player_two_.connection_id_);
                SendMessageToClient(ServerToClientSignifiers.ReMatchOfTicTacToeComplete + ",1", gr.player_one_.connection_id_);

               

                gr.agree_to_rematch_[0] = 0;
                gr.agree_to_rematch_[1] = 0;

                // clear out match data for new game
                gr.match_data_.Clear();
            }

        }
        else if (signifier == ClientToServerSignifiers.ExitTacTacToe)
        {
            GameRoom gr = GetGameRoomClientId(id);

            // ListOfgamerooms.Remove(gr);

            if (gr.player_one_.connection_id_ == id)
            {
                SendMessageToClient(ServerToClientSignifiers.PreventRematch + ",2", gr.player_two_.connection_id_);
                SendMessageToClient(ServerToClientSignifiers.ExitTacTacToeComplete + ",1", gr.player_one_.connection_id_);
                gr.player_one_.connection_id_ = -1;

                if (gr.observer_ != null)
                {
                    SendMessageToClient(ServerToClientSignifiers.PlayerDisconnectFromGameRoom + ",0", gr.observer_.connection_id_);
                }
            }
            else
            {
                SendMessageToClient(ServerToClientSignifiers.ExitTacTacToeComplete + ",2", gr.player_two_.connection_id_);
                SendMessageToClient(ServerToClientSignifiers.PreventRematch + ",1", gr.player_one_.connection_id_);
                gr.player_two_.connection_id_ = -1;

                if (gr.observer_ != null)
                {
                    SendMessageToClient(ServerToClientSignifiers.PlayerDisconnectFromGameRoom + ",0", gr.observer_.connection_id_);
                }
            }

            if (gr.player_one_.connection_id_ == -1 && gr.player_two_.connection_id_ == -1)
            {
                game_rooms.Remove(gr);
            }

        }
        else if (signifier == ClientToServerSignifiers.SearchGameRoomsByUserName)
        {
            if (GetGameRoomClientByUserName(csv[1]) == null)
            {
                SendMessageToClient(ServerToClientSignifiers.SearchGameRoomsByUserNameFailed + ",0", id);

            }
            else
            {
                GameRoom gr = GetGameRoomClientByUserName(csv[1]);

                if (gr.observer_ == null)
                {

                    if (gr.player_one_.name_ == csv[1])
                    {
                        SendMessageToClient(ServerToClientSignifiers.YouareBeingObserved + ",0", gr.player_one_.connection_id_);
                        SendMessageToClient(ServerToClientSignifiers.SearchGameRoomsByUserNameComplete + "," + gr.player_two_.name_.ToString(), id);
                        gr.observer_ = new PlayerAccount("Observer", id);
                    }
                    else
                    {
                        SendMessageToClient(ServerToClientSignifiers.YouareBeingObserved + ",0", gr.player_two_.connection_id_);
                        SendMessageToClient(ServerToClientSignifiers.SearchGameRoomsByUserNameComplete + "," + gr.player_one_.name_.ToString(), id);
                        gr.observer_ = new PlayerAccount("Observer", id);
                    }
                }
                else
                {
                    SendMessageToClient(ServerToClientSignifiers.SearchGameRoomsByUserNameSizeFailed + ",0", id);
                }
            }
        }
        else if (signifier == ClientToServerSignifiers.SendObserverData)
        {
            GameRoom gr = GetGameRoomClientId(id);
            SendMessageToClient(ServerToClientSignifiers.ObserverGetsMove + "," + csv[1] + "," + csv[2] + "," + csv[3] + "," + csv[4] + "," + csv[5] + "," + csv[6] + "," + csv[7] + "," + csv[8] + "," + csv[9] + "," + csv[10], gr.observer_.connection_id_);
            // Debug.LogWarning("SendObserverData" + "," + csv[1] + "," + csv[2] + "," + csv[3] + ",\n" + csv[4] + "," + csv[5] + "," + csv[7] + ",\n"+csv[6] + "," + csv[8] + "," + csv[9] + ",\n" + csv[10]);
        }
        else if (signifier == ClientToServerSignifiers.StopObserving)
        {
            GameRoom gr = GetGameRoomClientId(id);
            SendMessageToClient(ServerToClientSignifiers.YouAreNotBeingObserved + ",0", gr.player_one_.connection_id_);
            SendMessageToClient(ServerToClientSignifiers.YouAreNotBeingObserved + ",0", gr.player_two_.connection_id_);
            SendMessageToClient(ServerToClientSignifiers.StopObservingComplete + ",0", gr.observer_.connection_id_);
            gr.observer_ = null;

        }
        else if (signifier == ClientToServerSignifiers.SendGameRoomChatMSG)
        {
            GameRoom gr = GetGameRoomClientId(id);
            string game_room_chat_msg = csv[1];

            if (gr != null)
            {
                SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + game_room_chat_msg, gr.player_one_.connection_id_);
                SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + game_room_chat_msg, gr.player_two_.connection_id_);
                if (gr.observer_ != null)
                {
                    SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + game_room_chat_msg, gr.observer_.connection_id_);
                }
            }

        }
        else if (signifier == ClientToServerSignifiers.SendOnlyObserverGameRoomChatMSG)
        {
            GameRoom gr = GetGameRoomClientId(id);
            string game_room_chat_msg = csv[1];

            if (gr != null)
            {
                //SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + Msg, id);
                if (gr.observer_ != null)
                {
                    if (gr.observer_.connection_id_ == id)
                    {
                        SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + game_room_chat_msg, gr.observer_.connection_id_);
                    }
                    else if (gr.player_one_.connection_id_ == id || gr.player_two_.connection_id_ == id)
                    {
                        SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + game_room_chat_msg, gr.observer_.connection_id_);
                        SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + game_room_chat_msg, id);
                    }
                }

            }
        }
        else if (signifier == ClientToServerSignifiers.SendOnlyPlayerGameRoomChatMSG)
        {
            GameRoom gr = GetGameRoomClientId(id);
            string msg_from_game_room_chat_room = csv[1];

            if (gr != null)
            {
                if (gr.player_one_.connection_id_ == id)
                {
                    SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + msg_from_game_room_chat_room, gr.player_two_.connection_id_);
                    SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + msg_from_game_room_chat_room, id);
                }
                else if (gr.player_two_.connection_id_ == id)
                {
                    SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + msg_from_game_room_chat_room, gr.player_one_.connection_id_);
                    SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + msg_from_game_room_chat_room, id);
                }
                else if (gr.observer_.connection_id_ == id)
                {
                    SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + msg_from_game_room_chat_room, gr.player_one_.connection_id_);
                    SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + msg_from_game_room_chat_room, gr.player_two_.connection_id_);
                    SendMessageToClient(ServerToClientSignifiers.ReceiveGameRoomChatMSG + "," + msg_from_game_room_chat_room, id);

                }

            }
        }
        else if (signifier == ClientToServerSignifiers.CreateARecored)
        {
            Debug.Log("Player name: " + csv[1] + "RecordMatchName: " + csv[2]);
            bool isFileNameUnique = false;

            foreach (PlayerAccount pa in player_accounts)
            {
                //if(pa.name == csv[1]) 
                //{
                //     pa.recordMatchNames.AddLast(csv[2]);
                //}

                if (pa.record__names_ != null )
                {
                    // if  there account with a record
                    if (pa.record__names_.Count != 0)
                    {
                        foreach (string rmn in pa.record__names_)
                        {
                            if (csv[2] == rmn)
                            {
                                isFileNameUnique = false;
                                break;
                            }
                            else
                            {
                                isFileNameUnique = true;
                            }
                        }
                    }/// if there account with no record
                    else if (pa.record__names_.Count == 0) 
                    {
                        isFileNameUnique = true;
                    }
                }
            }
            if (isFileNameUnique == true)
            {
                foreach (PlayerAccount pa in player_accounts)
                {
                    if (pa.name_ == csv[1])
                    {
                        pa.record__names_.AddLast(csv[2]);
                        Debug.Log("Player Success to create FileNameUnique for record");


                        // Store the MatchData into a text file:
                        GameRoom gr = GetGameRoomClientId(id);
                        SaveMatchData(gr, csv[2]);
                        SendMessageToClient(ServerToClientSignifiers.CreateARecoredSuccess + ",0", id);
                        break;
                    }
                }
            }
            else
            {
                Debug.Log("Player fail to create FileNameUnique for record");
                SendMessageToClient(ServerToClientSignifiers.CreateARecoredFail + ",0", id);
            }
            SavePlayerManagementFile();
            //LoadPlayerManagementFile();
        }
        else if (signifier == ClientToServerSignifiers.AskForAllRecoreds)
        {
            foreach (PlayerAccount pa in player_accounts)
            {
                if (pa.name_ == csv[1])
                {
                    if (pa.record__names_.Count != 0) 
                    {
                        SendMessageToClient(ServerToClientSignifiers.StartSendAllRecoredsName + ",0", id);
                        foreach (string rmd in pa.record__names_)
                        {
                            SendMessageToClient(ServerToClientSignifiers.SendAllRecoredsNameData + "," + rmd, id);
                        }
                        SendMessageToClient(ServerToClientSignifiers.DoneSendAllRecoredsName + ",0", id); 
                    }
                    else 
                    {
                        // this player don't have a any match data/ record name
                        SendMessageToClient(ServerToClientSignifiers.NoRecordsNamefound + ",0", id);
                    }
                }
            }
        }
        else if (signifier == ClientToServerSignifiers.AskForThisRecoredMatchData) 
        {
        
            LinkedList<MatchData> matchDatas = new LinkedList<MatchData>();
            ReadSaveMatchData(csv[1], matchDatas);
            SendMessageToClient(ServerToClientSignifiers.StartSendThisRecoredMatchData + ",0", id);
            foreach (MatchData md in matchDatas)
            {
                SendMessageToClient(ServerToClientSignifiers.SendAllThisRecoredMatchData+ "," + md.player_name_ +","+ md.positoin_ + "," + md.player_symbol_, id);

            }
            SendMessageToClient(ServerToClientSignifiers.DoneSendAllThisRecoredMatchData + ",0", id);

        }
    }


    #region ReusebleCode

    /// <summary>
    ///  GetGameRoomClientId
    /// - Find a gameroom by Connection ID.
    /// - used in alot place where GameRoom involve
    /// </summary>
    private GameRoom GetGameRoomClientId (int id) 
    {
        foreach(GameRoom gr in game_rooms)
        {
            if (gr.player_one_.connection_id_ == id || gr.player_two_.connection_id_ == id ) 
            {
                return gr;
            }
            if(gr.observer_ != null) 
            {
                if(gr.observer_.connection_id_ == id) 
                {
                    return gr;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// GetGameRoomClientByUserName
    /// - it's use to find specifie gameroom  with certain players.
    /// - spectate mode use this funtion find players to view
    /// </summary>
    private GameRoom GetGameRoomClientByUserName(string user_name)
    {
        foreach (GameRoom gr in game_rooms)
        {
            if (gr.player_one_.name_ == user_name|| gr.player_two_.name_ == user_name)
            {
                return gr;
            }
        }
        return null;
    }

   
    /// <summary>
    /// NotifyPublicChatRoomUsersWithAMsg function used for Public chat
    /// - Send the msg to all clients in the Public chat.
    /// - not the gameroom chat
    /// </summary>

    private void NotifyPublicChatRoomUsersWithAMsg(string msg) 
    {
        foreach ( PlayerAccount pa in active_players_connected_global_chat)
        {
            SendMessageToClient(ServerToClientSignifiers.ChatView + "," + msg , pa.connection_id_);
        }
    }


    /// <summary>
    /// NotifyPublicChatClients function used for Public chat
    /// -Whenever player join/dicconnect/leave it will give the players in the PublicChat new list activePlayers
    /// -On cilent side its involve with list on player on side of chatroom
    /// - not the gameroom chat.
    /// </summary>

    private void NotifyPublicChatRoomClientsOfChanges()
    {
        // first loop makes sure the players in PublicChat connect id get names of all the player
        foreach (PlayerAccount cid in active_players_connected_global_chat)
        {
            // tell the client to clear thier list global chat player before updating it.
            SendMessageToClient(ServerToClientSignifiers.ReceiveClearListOFPlayerInChat + ",8", cid.connection_id_);

            // second loop is to give eacch person in list all the name in the list
            foreach (PlayerAccount pa in active_players_connected_global_chat) 
            {
                // sending update list of of player at are currently in global chat room to the client
                // pa will changes name in loop
                // well cid will not in this loop
                                                                                               
                SendMessageToClient(ServerToClientSignifiers.ReceiveListOFPlayerInChat + "," + pa.name_, cid.connection_id_);
            }
        }
    }

    

     /// <summary>
     /// GeneralDisconnected funtion is where all the disconnected code for
     /// - Discconnection from the Game Queue
     /// - Discconnection from the Public Chat room
     /// - Discconnection from the GameRoom
     /// </summary>


    private void GeneralDisconnected(int connection_id) 
    {
        // check if it's player in Queue
        if (player_waiting_for_match.connection_id_ == connection_id)
        {
            // reset  player_waiting_for_match.connection_id in connection id
            player_waiting_for_match.connection_id_ = -1;
            player_waiting_for_match.name_ = "TempPlayer";

            // break out the void funtion
            return;
        }


        //NotifyGlobalChatClients
        bool is_player_in_Chat = false;
        PlayerAccount temp_player_account = new PlayerAccount();

        // checking if the disconnect id  in the  public chat 

        foreach (PlayerAccount pa in active_players_connected_global_chat)
        {
            if (connection_id == pa.connection_id_)
            {
                temp_player_account = pa;
                is_player_in_Chat = true;
                break;
            }
        }

        /// if found in the public chat do this below
        if ( is_player_in_Chat) 
        {
            active_players_connected_global_chat.Remove(temp_player_account);

            NotifyPublicChatRoomClientsOfChanges();

            Debug.LogWarning("TempPlayerAccount : " + temp_player_account.connection_id_.ToString());

            string DisconnectMsg = "< " + temp_player_account.name_ + " > Have been disconnected from the chat.";

            NotifyPublicChatRoomUsersWithAMsg(DisconnectMsg);

            return; 
        }


        // check if the disconnect id is in the game room
        // game room

        bool is_game_room_vaild = false;

        /// check if this connection id is a game room
        GameRoom temp_gr = GetGameRoomClientId(connection_id);

        if (temp_gr != null )
        {
            is_game_room_vaild = true;
        }

        if (is_game_room_vaild == true)
        {

            if (temp_gr.player_one_.connection_id_ == connection_id)
            {
                Debug.LogWarning("A Player has disconnect");
                SendMessageToClient(ServerToClientSignifiers.PlayerDisconnectFromGameRoom + ",0", temp_gr.player_two_.connection_id_);
                if (temp_gr.observer_ != null)
                {
                    SendMessageToClient(ServerToClientSignifiers.PlayerDisconnectFromGameRoom + ",0", temp_gr.observer_.connection_id_);
                }
                game_rooms.Remove(temp_gr);
                return;
            }
            else if (temp_gr.player_two_.connection_id_ == connection_id)
            {
                SendMessageToClient(ServerToClientSignifiers.PlayerDisconnectFromGameRoom + ",0", temp_gr.player_one_.connection_id_);
                if (temp_gr.observer_ != null)
                {
                    SendMessageToClient(ServerToClientSignifiers.PlayerDisconnectFromGameRoom + ",0", temp_gr.observer_.connection_id_);

                }
                game_rooms.Remove(temp_gr);
                return;
            }
            else if (temp_gr.observer_.connection_id_ == connection_id)
            {
                Debug.LogWarning("Observer has disconnect");

                SendMessageToClient(ServerToClientSignifiers.YouAreNotBeingObserved + ",0", temp_gr.player_one_.connection_id_);
                SendMessageToClient(ServerToClientSignifiers.YouAreNotBeingObserved + ",0", temp_gr.player_two_.connection_id_);
                temp_gr.observer_ = null;
                return;
            }
           // is_this_room = false;
        }
    }


    

    #endregion




    #region Save/LoadFunctions

    public void LoadPlayerManagementFile()
    {
        if (File.Exists(Application.dataPath + Path.DirectorySeparatorChar + "PlayerManagementFile.txt"))
        {
            StreamReader sr = new StreamReader(Application.dataPath + Path.DirectorySeparatorChar + "PlayerManagementFile.txt");
            string line;
            PlayerAccount loaded_player = new PlayerAccount("TempName", "TempPass");

            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');

                int signifier = int.Parse(csv[0]);
                if (signifier == PlayerRecordManagementFileSignifiers.PlayerIdSignifier)
                {

                    loaded_player = new PlayerAccount(csv[1], csv[2]);
                    player_accounts.AddLast(loaded_player);
                }
                else if (signifier == PlayerRecordManagementFileSignifiers.PlayerRecordNameIdSignifier)
                {
                    loaded_player.record__names_.AddLast(csv[1]);
                }
            }
        }
    }
    public void SavePlayerManagementFile()
    {
        StreamWriter sw = new StreamWriter(Application.dataPath + Path.DirectorySeparatorChar + "PlayerManagementFile.txt");
        foreach (PlayerAccount pa in player_accounts)
        {
            sw.WriteLine(PlayerRecordManagementFileSignifiers.PlayerIdSignifier + "," + pa.name_ + "," + pa.password_);
            if (pa.record__names_ != null && pa.record__names_.Count != 0)
            {
                foreach (string rn in pa.record__names_)
                {
                    sw.WriteLine(PlayerRecordManagementFileSignifiers.PlayerRecordNameIdSignifier + "," + rn);
                }
            }
        }
        sw.Close();
    }

    public void ReadSaveMatchData(string file_name, LinkedList<MatchData> match_data)
    {
        if (File.Exists(Application.dataPath + Path.DirectorySeparatorChar + file_name + ".txt"))
        {
            StreamReader sr = new StreamReader(Application.dataPath + Path.DirectorySeparatorChar + file_name + ".txt");
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');

                int signifier = int.Parse(csv[0]);
                if (signifier == PlayerRecordManagementFileSignifiers.KEnumMatchDataIdSignifier)
                {

                    match_data.AddLast(new MatchData(csv[1], int.Parse(csv[2]), int.Parse(csv[3])));
                }
            }
        }
    }

    public void SaveMatchData(GameRoom gr, string file_name)
    {
        StreamWriter sw = new StreamWriter(Application.dataPath + Path.DirectorySeparatorChar + file_name + ".txt");
        foreach (MatchData matchData in gr.match_data_)
        {
            sw.WriteLine(PlayerRecordManagementFileSignifiers.KEnumMatchDataIdSignifier + "," + matchData.player_name_ + "," + matchData.positoin_ + "," + matchData.player_symbol_);
        }
        sw.Close();
    }

    #endregion



    #region MyClass
    public class GameRoom
    {
        
        public PlayerAccount  player_one_, player_two_;

        public PlayerAccount observer_ = null ;

        public LinkedList<MatchData> match_data_;

        public int[] agree_to_rematch_; 

        public GameRoom()
        {

        }
      
        public GameRoom(PlayerAccount player_id_one, PlayerAccount player_id_two)
        {
            player_one_ = player_id_one;

            player_two_ = player_id_two;

            match_data_ = new LinkedList<MatchData>();

            agree_to_rematch_ = new int[2] { 0, 0};
        }
        public void AddObserverGameRoom(PlayerAccount new_observer)
        {
            observer_ = new_observer;
        }

    }

    public class MatchData
    {
        public int positoin_;
        public int player_symbol_;
        public string player_name_;

        public MatchData(string player_name,int position, int player_symbol)
        {
            positoin_ = position;
            player_name_ = player_name;
            player_symbol_ = player_symbol;
        }

    }

    public class PlayerAccount
    {
        public string name_, password_;
        public int connection_id_;
        public LinkedList<string> record__names_;


        public PlayerAccount(string name, string password)
        {
            name_ = name;
            password_ = password;
            record__names_ = new LinkedList<string>(); 
        }
        public PlayerAccount(string name, string password, int coninection_id)
        {
            name_ = name;
            password_ = password;
            connection_id_ = coninection_id;
        }
        public PlayerAccount(string Name, int coninection_id)
        {
            name_ = Name;
            connection_id_ = coninection_id;
        }
        public PlayerAccount()
        {

        }

    }
    #endregion

}



#region Protocol

public class ClientToServerSignifiers
  {
    public const int CreateAcount = 1;

    public const int Login = 2;

    public const int SendChatMsg = 3; // send a globle chat message

    public const int SendChatPrivateMsg = 4;// send a chat private msg

    public const int EnterTheChatRoom = 5; // enter the chat room

    public const int Logout = 6;//

    public const int JoinQueueForGameRoom = 7;

    public const int TicTacToesSomethingSomthing = 8;

    public const int ReMatchOfTicTacToe = 9;

    public const int ExitTacTacToe = 10;

    public const int SearchGameRoomsByUserName = 11;

    public const int SendObserverData = 12;

    public const int StopObserving = 13;

    public const int SendGameRoomChatMSG = 14;

    public const int SendOnlyPlayerGameRoomChatMSG = 15;

    public const int SendOnlyObserverGameRoomChatMSG = 16;

    public const int CreateARecored = 17;

    public const int AskForAllRecoreds = 18;

    public const int AskForThisRecoredMatchData = 19;

}


public class ServerToClientSignifiers
{

    public const int LoginComplete = 1;

    public const int LoginFailedAccount = 2;

    public const int LoginFailedPassword = 3;

    public const int CreateAcountComplete = 4;

    public const int CreateAcountFailed = 5;

    public const int ChatView = 6; // all the receive globe chatmessage.

    public const int ReceivePrivateChatMsg = 7;//  receive a private chat message.

    public const int ReceiveListOFPlayerInChat = 8;// all the list of players in the chat

    public const int ReceiveClearListOFPlayerInChat = 9;// all the list of players 

    public const int LogOutComplete = 10;

    public const int OpponentPlayed = 11;

    public const int GameStart = 12;

    public const int WaitForOppentMoved = 13;

    public const int ReMatchOfTicTacToeComplete = 14;

    public const int ExitTacTacToeComplete = 15;

    public const int PreventRematch = 16;

    public const int SearchGameRoomsByUserNameComplete = 17;

    public const int SearchGameRoomsByUserNameFailed = 18;

    public const int YouareBeingObserved = 20;

    public const int ObserverGetsMove = 21;

    public const int YouAreNotBeingObserved = 22;

    public const int PlayerDisconnectFromGameRoom = 23;

    public const int StopObservingComplete = 24;

    public const int ReceiveGameRoomChatMSG = 25;

    public const int ReceiveOpponentName = 26;

    public const int SearchGameRoomsByUserNameSizeFailed = 27;

    public const int CreateARecoredSuccess = 28;

    public const int CreateARecoredFail = 29;

    public const int StartSendAllRecoredsName = 30;

    public const int SendAllRecoredsNameData = 31;

    public const int DoneSendAllRecoredsName = 32;

    public const int StartSendThisRecoredMatchData = 33;

    public const int SendAllThisRecoredMatchData = 34;

    public const int DoneSendAllThisRecoredMatchData = 35;

    public const int NoRecordsNamefound = 36;
}

public class PlayerRecordManagementFileSignifiers 
{
    public const int PlayerIdSignifier = 1;

    public const int PlayerRecordNameIdSignifier = 50;

    public const int KEnumMatchDataIdSignifier = 51;


};


/*
 * naming convention:

-Enumerator Names->kEnumName
-Funtion-> AddTableEntry
-Constant Names -> kDaysInAWeek
-Class Data Members -> pool_
-Struct Data Members->table_name
-Variable -> table_name
 * 
 * 
 */
#endregion