﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Text;
using UnityEditor.Networking.PlayerConnection;
using UnityEngine.Networking.PlayerConnection;
using UnityEditor.IMGUI.Controls;
using System.IO;

namespace Sabresaurus.Sidekick
{
    public class SidekickRemoteWindow : EditorWindow
    {
        string lastMessage = "";

        string customMessage = "";

        bool localDevMode = false;


        [SerializeField] TreeViewState m_TreeViewState;

        SimpleTreeView m_TreeView;
        SearchField m_SearchField;


        [MenuItem("Sidekick/Remote Window")]
        static void Init()
        {
            SidekickRemoteWindow window = (SidekickRemoteWindow)EditorWindow.GetWindow(typeof(SidekickRemoteWindow));
            window.Show();
            window.titleContent = new GUIContent("Sidekick Remote");
        }

        void OnEnable()
        {
            EditorConnection.instance.Initialize();
            EditorConnection.instance.Register(RuntimeSidekick.kMsgSendPlayerToEditor, OnMessageEvent);

            // Check if we already had a serialized view state (state 
            // that survived assembly reloading)
            if (m_TreeViewState == null)
                m_TreeViewState = new TreeViewState();

            m_TreeView = new SimpleTreeView(m_TreeViewState);
            m_SearchField = new SearchField();
            m_SearchField.downOrUpArrowKeyPressed += m_TreeView.SetFocusAndEnsureSelectedItem;
        }

        void OnDisable()
        {
            EditorConnection.instance.Unregister(RuntimeSidekick.kMsgSendPlayerToEditor, OnMessageEvent);
            EditorConnection.instance.DisconnectAll();
        }

        private void OnMessageEvent(MessageEventArgs args)
        {
            using (MemoryStream ms = new MemoryStream(args.data))
            {
                using (BinaryReader br = new BinaryReader(ms))
                {
                    string action = br.ReadString();
                    string remainder = br.ReadString();

                    lastMessage = remainder;
                    Debug.Log("Message from player: " + lastMessage);
                    if (action == "GetHierarchy")
                    {
                        string[] split = remainder.Split('\n');
                        List<TreeViewItem> displays = new List<TreeViewItem>(split.Length);
                        for (int i = 0; i < split.Length; i++)
                        {
                            string trimmed = split[i].TrimStart('-');
                            int trimDepth = split[i].Length - trimmed.Length;
                            displays.Add(new TreeViewItem { id = i + 1, depth = trimDepth, displayName = trimmed });
                        }
                        m_TreeView.SetDisplays(displays);
                    }
                }
            }
        }


        void OnGUI()
        {
            localDevMode = EditorGUILayout.Toggle("Local Dev Mode", localDevMode);

            var playerCount = EditorConnection.instance.ConnectedPlayers.Count;
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(string.Format("{0} players connected.", playerCount));
            int count = 0;
            foreach (var p in EditorConnection.instance.ConnectedPlayers)
            {
                builder.AppendLine(string.Format("[{0}] - {1} {2}", count++, p.name, p.playerId));
            }
            EditorGUILayout.HelpBox(builder.ToString(), MessageType.Info);

            if (GUILayout.Button("GetHierarchy"))
            {
                SendToPlayers("GetHierarchy");
            }

            if (GUILayout.Button("GetGameObject"))
            {
                IList<int> selection = m_TreeView.GetSelection();
                if (selection.Count >= 1)
                {
                    IList<TreeViewItem> items = m_TreeView.GetRows();
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i].id == selection[0])
                        {
                            // Get the path of the selection
                            string path = GetPathForTreeViewItem(items[i]);
                            //Debug.Log(TransformHelper.GetFromPath(path).name);
                            SendToPlayers("GetGameObject", path);
                            break;
                        }
                    }
                }
            }

            //customMessage = EditorGUILayout.TextField("Custom", customMessage);

            if (GUILayout.Button("Send"))
            {
                //SendToPlayers(customMessage);
            }

            EditorGUILayout.TextArea(lastMessage, GUILayout.ExpandHeight(true), GUILayout.MinHeight(300));
            DoToolbar();
            DoTreeView();
        }

        static string GetPathForTreeViewItem(TreeViewItem item)
        {
            string path = item.displayName;

            item = item.parent;
            while (item != null && item.depth >= 0)
            {
                path = item.displayName + "/" + path;
                item = item.parent;
            }

            return path;
        }

        void SendToPlayers(string action, params string[] args)
        {
            byte[] bytes;
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write("1");

                    bw.Write(action);
                    foreach (var item in args)
                    {
                        bw.Write(item);
                    }
                }
                bytes = ms.ToArray();
            }
            if (localDevMode)
            {
                var testResponse = SidekickRequestProcessor.Process(bytes);
                MessageEventArgs messageEvent = new MessageEventArgs();
                messageEvent.data = testResponse;
                OnMessageEvent(messageEvent);
            }
            else
            {
                EditorConnection.instance.Send(RuntimeSidekick.kMsgSendEditorToPlayer, bytes);
            }
        }


        void DoToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(100);
            GUILayout.FlexibleSpace();
            m_TreeView.searchString = m_SearchField.OnToolbarGUI(m_TreeView.searchString);
            GUILayout.EndHorizontal();
        }

        void DoTreeView()
        {
            Rect rect = GUILayoutUtility.GetRect(0, 100000, 0, 100000);
            m_TreeView.OnGUI(rect);
        }
    }
}