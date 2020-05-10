// Copyright (c) 2015 Z-Software GmbH
// http://www.z-software.net/
// Author: Oliver Iking
// 
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// 
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// 
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
// The following version is modified by Ricky Leung (rekize@gmail.com)

using System.Collections.Generic;
using AC;
using UnityEditor;
using UnityEngine;

namespace ZS.Tools.Editor
{
	/// <summary>
	/// Mass editor for speech lines in the current scene
	/// </summary>
	public class SpeechActionMassEditor : EditorWindow
	{
		// Add menu named "My Window" to the Window menu
		[MenuItem("Window/Speech Line Mass Editor")]
		static void Open()
		{
			// Get existing open window or if none, make a new one:
			SpeechActionMassEditor window = (SpeechActionMassEditor)EditorWindow.GetWindow(typeof(SpeechActionMassEditor));
			window.Show();
		}

		/// <summary>
		/// Small helper class which contains info about any discovered speech action
		/// </summary>
		[System.Serializable]
		class SpeechLine : System.IComparable<SpeechLine>
		{
			/// <summary>
			/// Wrapped ActionSpeech for Undo-supported edits
			/// </summary>
			public SerializedObject SerializedActionSpeech;
			public ActionList ContainedList;
			public int ActionIndex;
			public string SpeechOrder;

			// For sorting the SpeechLine class according to SpeechOrder values
			public int CompareTo(SpeechLine SL)
			{       // A null value means that this object is greater.
				if (SL == null){
					return 1;  
				}
				else {
					return this.SpeechOrder.CompareTo(SL.SpeechOrder);
				}
			}
		}

		/// <summary>
		/// List of recently discovered speech actions
		/// </summary>
		[SerializeField]
		List<SpeechLine> discoveredLines = new List<SpeechLine>();

		/// <summary>
		/// List of filtered speech actions
		/// </summary>
		[SerializeField]
		List<SpeechLine> displayedLines = new List<SpeechLine>();

		/// <summary>
		/// Scroll position for the speech
		/// </summary>
		[SerializeField]
		Vector2 scrollPosition = Vector2.zero;

		/// <summary>
		/// Text to filter the speech for
		/// </summary>
		/// <remarks>
		/// Stored in editor prefs
		/// </remarks>
		string currentTextFilter
		{
			set
			{
				EditorPrefs.SetString("SpeechActionMassEditor.currentTextFilter", value);
			}

			get
			{
				return EditorPrefs.GetString("SpeechActionMassEditor.currentTextFilter", string.Empty);
			}
		}

		/// <summary>
		/// Window got enabled
		/// </summary>
		void OnEnable()
		{
			// Do a initial search for speech
			FindSpeech();
		}

		/// <summary>
		/// Editor selection changed
		/// </summary>
		void OnSelectionChange()
		{
			// Just automatically refresh the speech when the selection changes, user
			// potentially added / removed actions?
			FindSpeech();
		}

		/// <summary>
		/// Updates displayed lines
		/// </summary>
		void UpdateDisplayedLines()
		{
			displayedLines.Clear();
			for (int n = 0; n < discoveredLines.Count; n++)
			{
				if (DoShow(discoveredLines[n]))
				{
					displayedLines.Add(discoveredLines[n]);
				}
			}

			// Sort the speechlines by SpeechOrder
			displayedLines.Sort ();
		}

		/// <summary>
		/// Rejects speech lines when the current filter does not apply
		/// </summary>
		/// <param name="line">Line to filter</param>
		/// <returns>True when the line should be considered</returns>
		bool DoShow(SpeechLine line)
		{
			if (string.IsNullOrEmpty(currentTextFilter))
			{
				return true;
			}

			var speechAction = (line.ContainedList.actions[line.ActionIndex] as ActionSpeech);

			if (speechAction.messageText.Contains(currentTextFilter))
			{
				return true;
			}

			if (speechAction.speaker != null && speechAction.speaker.speechLabel.Contains(currentTextFilter))
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Draws actual editor window UI
		/// </summary>
		void OnGUI()
		{
			GUILayout.Label("Gather text from Adventure Creator Speech Manager before using this editor");

			// Button for manual refresh
			if (GUILayout.Button(new GUIContent("Refresh", null, "Manually refresh the list of speech below")))
			{
				FindSpeech();
			}

			// Some informational output about found speech
			GUILayout.Label(string.Format("Showing {0} of {1} speech items", displayedLines.Count, discoveredLines.Count));

			GUILayout.BeginHorizontal();
			EditorGUI.BeginChangeCheck();
			currentTextFilter = EditorGUILayout.TextField("Filter", currentTextFilter);
			if (EditorGUI.EndChangeCheck())
			{
				UpdateDisplayedLines();
			}
			if (GUILayout.Button(new GUIContent("X", null, "Clear filter"), GUILayout.Width(20F)))
			{
				currentTextFilter = string.Empty;
				UpdateDisplayedLines();
			}
			GUILayout.EndHorizontal();

			// Scroll area for all the individual speech
			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

			Color origBack = GUI.backgroundColor;

			if (displayedLines.Count == 0)
			{
				GUILayout.Label("No speech actions found");
			}

			for (int n = 0; n < displayedLines.Count; n++)
			{
				if (displayedLines[n].SerializedActionSpeech.targetObject == null)
				{
					FindSpeech();
					break;
				}

				// Synchronize SO
				displayedLines[n].SerializedActionSpeech.Update();

				EditorGUI.BeginChangeCheck();

				// Color each row differently for easier visual guidance (but only the background box!)
				GUI.backgroundColor = n % 2 == 0 ? Color.blue : Color.green;
				EditorGUILayout.BeginVertical("Box");
				GUILayout.Space(3F);
				GUI.backgroundColor = origBack;

				var propIsPlayer = displayedLines[n].SerializedActionSpeech.FindProperty("isPlayer");
				var propSpeaker = displayedLines[n].SerializedActionSpeech.FindProperty("speaker");
				var prop = displayedLines[n].SerializedActionSpeech.FindProperty("messageText");
				var propSpeechOrder = displayedLines[n].SpeechOrder; // Get the speech order

				var speaker = (propSpeaker.objectReferenceValue as Char);

				EditorGUILayout.BeginHorizontal();
				GUILayout.Label(propIsPlayer.boolValue ? "Player" : (speaker == null ? "Narration" : speaker.speechLabel), GUILayout.Width(150F));
				GUILayout.Label(propSpeechOrder, GUILayout.Width(300F)); // Display the speech order
				prop.stringValue = EditorGUILayout.TextArea(prop.stringValue);
				if (GUILayout.Button("Goto", GUILayout.Width(100F)))
				{
					ActionListEditorWindow.Init(displayedLines[n].ContainedList);
				}
				EditorGUILayout.EndHorizontal();

				GUILayout.Space(3F);
				EditorGUILayout.EndVertical();

				// When something changed, apply SO props and generate a undo action
				if (EditorGUI.EndChangeCheck())
				{
					Undo.SetCurrentGroupName("Text modification");
					displayedLines[n].SerializedActionSpeech.ApplyModifiedProperties();

					// Manually set it all dirty
					EditorUtility.SetDirty(displayedLines[n].ContainedList);
					EditorUtility.SetDirty(displayedLines[n].SerializedActionSpeech.targetObject);
				}
			}

			EditorGUILayout.EndScrollView();
		}

		/// <summary>
		/// Looks for all speech in the current scene
		/// </summary>
		private void FindSpeech()
		{
			discoveredLines.Clear ();

			ActionList[] listsInScene = Object.FindObjectsOfType<ActionList> ();

			foreach (ActionList al in listsInScene) {
				for (int n = 0; n < al.actions.Count; n++) {
					if (al.actions [n] is ActionSpeech) {
						discoveredLines.Add (new SpeechLine {
							SerializedActionSpeech = new SerializedObject (al.actions [n]),
							ContainedList = al,
							ActionIndex = n,
							SpeechOrder = KickStarter.speechManager.GetLine (((ActionSpeech)al.actions [n]).lineID).OrderIdentifier
						});
					}
				}
			}

			// Choose all matching lines for display now
			UpdateDisplayedLines();

			Repaint();
		}
	}
}
