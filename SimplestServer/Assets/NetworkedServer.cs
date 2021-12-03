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



        /*
         - initialzing my global variables
         */

        game_rooms = new LinkedList<GameRoom>();

        active_players_connected_global_chat = new LinkedList<PlayerAccount>();

        player_accounts = new LinkedList<PlayerAccount>();

     

        // reads in player accounts all store in text at beginning
        //
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
                
                break;
            case NetworkEventType.DataEvent:

                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);

                ProcessRecievedMsg(msg, recConnectionID);

                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);

                //- this where  GeneralDisconnected get the disconnection id from
                GeneralDisconnected(recConnectionID);

           
                break;
        }

    }

    /// <summary>
    /// SendMessageToClient
    /// - its the fuction thats send data back to client
    /// </summary>
    /// 
    /// <param name="msg"></param>
    /// - the data we want to send out.
    /// 
    /// <param name="id"></param>
    /// -specific Connection id you want to send the data to.
    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }

    /// <summary>
    /// ProcessRecievedMsg
    /// -it where I processsing the data from clients.
    /// - signifier help determine what data what kind of data im get from the client and what to do with it.
    /// </summary>
    /// 
    /// <param name="msg"></param>
    /// -msg is the data we get from the clients
    /// 
    /// <param name="id"></param>
    ///  - id is Connection id
    ///  - it helps to identify the clinet
    ///  - help differentiate clients
    ///  
    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');

        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToServerSignifiers.CreateAcount)
        {
            ///- got a  request to create a new player account

          

            Debug.Log("create an Account");
            // check if player  account name already exists,

            // local Variables
            // extracting  the data
            bool is_name_in_use = false;
            string user_name = csv[1];
            string password = csv[2];


            //-- check if there a player Account with that username 
            foreach (PlayerAccount pa in player_accounts)
            {
                if (pa.name_ == user_name)
                {
                    is_name_in_use = true;
                    break;
                }
            }

            //--if there is  player account with that name user name
            // -- send a msg back if telling client the user is in invaild
            if (is_name_in_use)
            {
                SendMessageToClient(ServerToClientSignifiers.CreateAcountFailed + ",8", id);
                Debug.LogWarning("This Account already exist");
            }
            else
            {
                //--if there isn't  player account with that name user name
                // -- send a msg back if telling client the user is vaild

                // Create  new account, add to list

                PlayerAccount newPlayAccount = new PlayerAccount(user_name, password);
                player_accounts.AddLast(newPlayAccount);
                SendMessageToClient(ServerToClientSignifiers.CreateAcountComplete + ",8", id);

                // update the PlayerManagementFile text
                //  rewrite it
                SavePlayerManagementFile();
                Debug.LogWarning("This Account has created and add");
            }
            // If not, 
            // send to success/ failure
        }
        else if (signifier == ClientToServerSignifiers.Login)
        {
            ///- got a  request Login with this data
            ///

            // Login(csv[1], csv[2], id);

            Debug.Log("Login to an account");

            // local Variables
            // extracting  the data

            bool is_name_in_use = false;
            bool is_passward_in_use = false;
            string user_name = csv[1];
            string password = csv[2];


            // check if player  account name already exists,
            foreach (PlayerAccount pa in player_accounts)
            {
                if (pa.name_ == user_name)
                {
                    is_name_in_use = true;
                    break;
                }
            }

            //  if there is an player Accont
            if (is_name_in_use)
            {
                //  check the pasword 
                foreach (PlayerAccount pa in player_accounts)
                {
                    if (pa.name_ == user_name && pa.password_ == password)
                    {
                        is_passward_in_use = true;
                        break;
                    }
                }

                // Both user name and password are correct
                if (is_passward_in_use)
                {
                    Debug.LogWarning("Password was right. You are in your Account");
                    SendMessageToClient(ServerToClientSignifiers.LoginComplete + "," + user_name, id);

                }
                else
                {
                    // password was incorrect
                    SendMessageToClient(ServerToClientSignifiers.LoginFailedPassword + ",8", id);
                    Debug.LogWarning("Password was wrong");
                }
            }
            else
            {
                // user name isnt in our database
                Debug.LogWarning("This Account doesn't exist");
                SendMessageToClient(ServerToClientSignifiers.LoginFailedAccount + ", 8", id);
            }

            // send to success/ failure
        }
        else if (signifier == ClientToServerSignifiers.SendChatMsg)
        {
            ///- got a request send a msg to Public chat room with this data
            /// extracting the msg
            string Msg = csv[1];

            // send the msg all in Public chat rule
            NotifyPublicChatRoomUsersWithAMsg(Msg);
        }
        else if (signifier == ClientToServerSignifiers.EnterTheChatRoom)
        {
            // - got a request add the Public chat room 

          
            // get the user name from the msg
            string user_name = csv[1];

            // add this player to list player in public chat room
            active_players_connected_global_chat.AddLast(new PlayerAccount(user_name, id));
           

            // send the  join chat msg and send it all in public chat
            string join_chat_msg = "< " + user_name + " > Have just join the chat.";
            NotifyPublicChatRoomUsersWithAMsg(join_chat_msg);

            // Update list of player and options of players for private messaging in Public chat room.
            // so you can see who in the chat.
            NotifyPublicChatRoomClientsOfChanges();
        }
        else if (signifier == ClientToServerSignifiers.Logout)
        {
            // note to self  rename Logout signifier 
            // check if it still works 

            //- got a request to log out a player of the Public chat room


            // check if this player is in the Public chat room
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

          
            Debug.LogWarning("TempPlayerAccount : " + temp_player_account.connection_id_.ToString());

            /// if found  do the follow
            ///  remove player with list of players in Public chat chat room
            ///  create a logot msg and send it all players in Public chat room
            ///  Update clients  on who in the Public chat (NotifyPublicChatRoomClientsOfChanges());
            ///  send the logout player client the msg to exit them out
            if (is_player_in_chat)
            {

                active_players_connected_global_chat.Remove(temp_player_account);

                string LogOutMsgOfChat = "< " + temp_player_account.name_ + " > Has Logout.";
                NotifyPublicChatRoomUsersWithAMsg(LogOutMsgOfChat);

                LogOutMsgOfChat = ServerToClientSignifiers.LogOutComplete + ",8";

                

                NotifyPublicChatRoomClientsOfChanges();

                SendMessageToClient(LogOutMsgOfChat, id);
                return;

            }

        }
        else if (signifier == ClientToServerSignifiers.SendChatPrivateMsg)
        {
            // note to self  rename SendChatPrivateMsg signifier 
            // check if it still works 
            //- got a request to send a private message to someone in Public chat 


            // extracting the data
            string global_chat_pm = csv[1]; // the msg we want send
            string user_name = csv[2]; // username of person in public chat we want to send it to.

            PlayerAccount specifier_player_in_chat = new PlayerAccount();
            bool is_player_real = false;


            // check if the user we want to send it is in Public chat
            // getting the player account and it's Connection id
            foreach (PlayerAccount pa in active_players_connected_global_chat)
            {
                if (pa.name_ == user_name)
                {
                    specifier_player_in_chat = pa;
                    is_player_real = true;
                    break;
                }
            }

            // if the is user in the Public chat.
            if (is_player_real)
            {
                // this code here to prevent  duplication.
                // we don't want keep send the same msg multiple times
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
            //- got a request to play a game/

            Debug.Log(" We need to get this player in a waiting Queue!!!");

            //check if someone else isn't already in the queue by check id number
            // side note: the first player in  the queue gets move first when the game start
            if (player_waiting_for_match.connection_id_ == -1)
            {
                // put get they're  data 
                // hold it in the temporary globle Player account
                player_waiting_for_match.connection_id_ = id;
                player_waiting_for_match.name_ = csv[1];
            }
            else
            {
                // if you're the second player in queue.
                // we are going create a game room and place you in it.
                //  but if the first person in the queue  isconnects before someone else joins
                //  (GeneralDisconnected() will rest temporary globle Player account if there is disconnects)
                // you become the first person in the queue.

                if (player_waiting_for_match.connection_id_ != -1) // check if first player in queue disconnect
                {
                    GameRoom gr = new GameRoom(player_waiting_for_match, new PlayerAccount(csv[1], id));
                    game_rooms.AddLast(gr);


                    SendMessageToClient(ServerToClientSignifiers.ReceiveOpponentName + "," + gr.player_one_.name_, gr.player_two_.connection_id_);
                    SendMessageToClient(ServerToClientSignifiers.ReceiveOpponentName + "," + gr.player_two_.name_, gr.player_one_.connection_id_);

                    SendMessageToClient(ServerToClientSignifiers.GameStart + ", 2", gr.player_two_.connection_id_);
                    SendMessageToClient(ServerToClientSignifiers.GameStart + ", 1", gr.player_one_.connection_id_);

                    //rest the globle Player account after placing the players into gameroom
                    player_waiting_for_match = new PlayerAccount("TempPlayer", -1);
                }
            }
        }
        else if (signifier == ClientToServerSignifiers.TicTacToesSomethingSomthing)
        {

            // note to self  rename TicTacToesSomethingSomthing signifier 
            // check if it still works 

            //- got a request to do a move on the  TicTacToesboard

            // find the game room
            GameRoom gr = GetGameRoomClientId(id);
            if (gr != null)
            {
                // check which player made the move
                // - then tell the player who moved to wait and send the move to your opponent.
                // - will do the same when your opponent moves.
                // - store the data of the move into gameroom's match data.
                // -this is turn base and not real time
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
               
            }

        }
        else if (signifier == ClientToServerSignifiers.ReMatchOfTicTacToe)
        {
            //- got a request to  the ReMatch the TicTacToe game

            // find game room
            GameRoom gr = GetGameRoomClientId(id);

            // check which player argee to a rematch 
            if (gr.player_one_.connection_id_ == id)
            {
                gr.agree_to_rematch_[0] = 1;
            }
            else
            {
                gr.agree_to_rematch_[1] = 1;
            }

            // if both players argee to rematch
            // -send a msg to reset the to TicTacToe board
            // - reset the argee int
            // clear the past match data
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
            //- got a request to the ExitTacTacToe

            // find game room
            GameRoom gr = GetGameRoomClientId(id);

            // send a msg to the player who request it and log them out
            //  stop the other player for  requesting a rematch( they're rematch button isn't use able)
            //  and  disconnect the observer from the GameRoom if there is one.
            // if there is no players in the gameroom it will be remove
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
            //- got a request to view a gameroom

            // send this msg back if there no player with that usernamme in the list of gamerooms
            if (GetGameRoomClientByUserName(csv[1]) == null)
            {
                SendMessageToClient(ServerToClientSignifiers.SearchGameRoomsByUserNameFailed + ",0", id);

            }
            else
            {
                // found the game room with user
                GameRoom gr = GetGameRoomClientByUserName(csv[1]);

                // check if isn't already be view by someone
                if (gr.observer_ == null)
                {
                    // check if which player is be view.
                    // -> will board data to server to be sent to observer.
                    if (gr.player_one_.name_ == csv[1])
                    {
                        SendMessageToClient(ServerToClientSignifiers.YouareBeingObserved + ",0", gr.player_one_.connection_id_);
                        SendMessageToClient(ServerToClientSignifiers.SearchGameRoomsByUserNameComplete + "," + gr.player_two_.name_.ToString(), id);
                        gr.observer_ = new PlayerAccount("Observer", id);
                        gr.view_player_connection_id_ = gr.player_one_.connection_id_;
                    }
                    else
                    {
                        SendMessageToClient(ServerToClientSignifiers.YouareBeingObserved + ",0", gr.player_two_.connection_id_);
                        SendMessageToClient(ServerToClientSignifiers.SearchGameRoomsByUserNameComplete + "," + gr.player_one_.name_.ToString(), id);
                        gr.observer_ = new PlayerAccount("Observer", id);
                        gr.view_player_connection_id_ = gr.player_two_.connection_id_;
                    }
                }
                else
                {
                    // already be view by someone
                    // size at max view (is one)
                    SendMessageToClient(ServerToClientSignifiers.SearchGameRoomsByUserNameSizeFailed + ",0", id);
                }
            }
            // side note:
            // this code is old and can update..( this code was made  before sharingRoom class )-> learned a lot from that class about game room.
            // this code was made during read week and before I realize correct due of assignment.
            // it can be redesign: gr could send all matchdata to observers.
            // gr would have be redesign with list of observers instead just one
            // -A way to check that list of observers isn't empty
            // - send a move to Observers every time players makes a move
        }
        else if (signifier == ClientToServerSignifiers.SendObserverData)
        {
            //- got a request from the view player to send board data to the observer

            GameRoom gr = GetGameRoomClientId(id);

            SendMessageToClient(ServerToClientSignifiers.ObserverGetsMove + "," + csv[1] + "," + csv[2] + "," + csv[3] + "," + csv[4] + "," + csv[5] + "," + csv[6] + "," + csv[7] + "," + csv[8] + "," + csv[9] + "," + csv[10], gr.observer_.connection_id_);
           
            // Debug.LogWarning("SendObserverData" + "," + csv[1] + "," + csv[2] + "," + csv[3] + ",\n" + csv[4] + "," + csv[5] + "," + csv[7] + ",\n"+csv[6] + "," + csv[8] + "," + csv[9] + ",\n" + csv[10]);
        }
        else if (signifier == ClientToServerSignifiers.StopObserving)
        {
            //-  got a request from observer to watching

            // check if which player is view be viewd
            // send msg to view player to stop sending move to observer
            // log out the observer
            // set observer to null
            GameRoom gr = GetGameRoomClientId(id);
            if (gr.player_one_.connection_id_ == gr.view_player_connection_id_)
            {
                SendMessageToClient(ServerToClientSignifiers.YouAreNotBeingObserved + ",0", gr.player_one_.connection_id_);
                SendMessageToClient(ServerToClientSignifiers.StopObservingComplete + ",0", gr.observer_.connection_id_);
                gr.observer_ = null;
            }
            else if (gr.player_two_.connection_id_ == gr.view_player_connection_id_) 
            {
                SendMessageToClient(ServerToClientSignifiers.YouAreNotBeingObserved + ",0", gr.player_two_.connection_id_);
                SendMessageToClient(ServerToClientSignifiers.StopObservingComplete + ",0", gr.observer_.connection_id_);
                gr.observer_ = null;
            }

        }
        else if (signifier == ClientToServerSignifiers.SendGameRoomChatMSG)
        {
            // note to self  rename SendGameRoomChatMSG signifier 
            // check if it still works 

            //- got a request to send a message from a gameroom for everyone in gameroom to see

            // find the gameroom
            GameRoom gr = GetGameRoomClientId(id);

            // get the msg
            string game_room_chat_msg = csv[1];
            
            /// if the gr exist
            /// send the msg all in the gameroom
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
            //- got a request to send a message from a gameroom for only the observer in gameroom to see

            // find the game room
            GameRoom gr = GetGameRoomClientId(id);
            // grab the msg
            string game_room_chat_msg = csv[1];

            // if the gr exists
            // check if there is obsever and send them the msg
            // the next if statement is there to  prevent  duplicates msg

            if (gr != null)
            {
                
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
            //- got a request to send a message from a gameroom for only the players in gameroom to see

            // find the game room
            GameRoom gr = GetGameRoomClientId(id);

            // grab the msg
            string msg_from_game_room_chat_room = csv[1];

            // if the gr exists
            // the next if statement is there to  prevent  duplicates msg
            // send the msg to the players
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
           // Debug.Log("Player name: " + csv[1] + "RecordMatchName: " + csv[2]);

            ///- 
            bool isFileNameUnique = false;

            foreach (PlayerAccount pa in player_accounts)
            {
                

                if (pa.record__names_ != null )
                {
                    // if  there account with a record
                    if (pa.record__names_.Count != 0)
                    {
                        foreach (string rmn in pa.record__names_)
                        {
                            if (csv[2] == rmn)
                            {
                                // if we a record with that name we want to break of out the foreach
                                isFileNameUnique = false;
                                break;
                            }
                            else
                            {
                                // if we don't find a record with that name
                                // player will be able a create record with that
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
            //there is no record with that name
            // create record under this playerAccount
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
            else //there is  record with that name
            {
                // send a msg that we already a reacord with that name
                Debug.Log("Player fail to create FileNameUnique for record");
                SendMessageToClient(ServerToClientSignifiers.CreateARecoredFail + ",0", id);
            }

            // update PlayerManagementFile txt file
            // this might need to be moveV
            SavePlayerManagementFile();
           
        }
        else if (signifier == ClientToServerSignifiers.AskForAllRecoreds)
        {
            //- got a request to send all record name underneath this playerAccount.

            // looking for a player Account 
            foreach (PlayerAccount pa in player_accounts)
            {
                // if we find that player account
                if (pa.name_ == csv[1])
                {
                    // if check there any records
                    if (pa.record__names_.Count != 0) 
                    {
                        // send the client a msg that we're about to send all record name
                        // send all record name to client
                        // send a msg that we are done sending all the record name to the client

                        SendMessageToClient(ServerToClientSignifiers.StartSendAllRecoredsName + ",0", id);
                        foreach (string rmd in pa.record__names_)
                        {
                            SendMessageToClient(ServerToClientSignifiers.SendAllRecoredsNameData + "," + rmd, id);
                        }
                        SendMessageToClient(ServerToClientSignifiers.DoneSendAllRecoredsName + ",0", id); 
                    }
                    else // there was no records
                    {
                        // this player don't have a any match data/ record name
                        SendMessageToClient(ServerToClientSignifiers.NoRecordsNamefound + ",0", id);
                    }
                }
            }
        }
        else if (signifier == ClientToServerSignifiers.AskForThisRecoredMatchData) 
        {
            //- got a request for Matchdata

            // temporary list of  match data
            LinkedList<MatchData> match_datas = new LinkedList<MatchData>();

            // read the text data
            // we are pass in text file name and  list of match data
            // we'll get all mata data from  that  text fill in the temporary list of  match data
            ReadSaveMatchData(csv[1], match_datas);

            // send the client a msg that we're about to send all match data
            // send all  match data to client
            // send a msg that we are done sending all the match data to the client

            SendMessageToClient(ServerToClientSignifiers.StartSendThisRecoredMatchData + ",0", id);
            foreach (MatchData md in match_datas)
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
    /// - used in alot place where GameRooms are involve
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
        // - Discconnection from the Game Queue
        // check if it's player in Queue
        if (player_waiting_for_match.connection_id_ == connection_id)
        {
            // reset  player_waiting_for_match.connection_id in connection id
            player_waiting_for_match.connection_id_ = -1;
            player_waiting_for_match.name_ = "TempPlayer";

            // break out the void funtion
            return;
        }

        // - Discconnection from the Public Chat room
        //check if it's player  in Public Chat room
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

        /// - Discconnection from the GameRoom
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
          
        }
    }




    #endregion




    #region Save/LoadFunctions
    /// <summary>
    /// - Save/LoadFunctions
    /// -This is code involve with storing data as txt
    /// - also involve reading the text
    /// </summary>
    /// 


    ///<summary>
    ///-LoadPlayerManagementFile()
    ///- checks if PlayerManagement text exist.
    ///- read the data from it.
    ///- use  PlayerRecordManagementFileSignifiers find what data it is.
    ///-  place the data as new PlayerAccont and add it to (List of PlayerAccount)player_accounts
    /// </summary>
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

    /// <summary>
    /// -SavePlayerManagementFile()
    /// - Create a Text file named PlayerManagementFile.
    /// - Writes data into the text for us to read later on.
    /// -use PlayerRecordManagementFileSignifiers is use to tell what data what are looking at
    /// </summary>
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="file_name"></param>
    /// <param name="match_datas"></param>
    public void ReadSaveMatchData(string file_name, LinkedList<MatchData> match_datas)
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

                    match_datas.AddLast(new MatchData(csv[1], int.Parse(csv[2]), int.Parse(csv[3])));
                }
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="gr"></param>
    /// <param name="file_name"></param>
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

        public int view_player_connection_id_;

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