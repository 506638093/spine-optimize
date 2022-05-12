/******************************************************************************
 * Spine Runtimes Software License
 * Version 2.1
 * 
 * Copyright (c) 2013, Esoteric Software
 * All rights reserved.
 * 
 * You are granted a perpetual, non-exclusive, non-sublicensable and
 * non-transferable license to install, execute and perform the Spine Runtimes
 * Software (the "Software") solely for internal use. Without the written
 * permission of Esoteric Software (typically granted by licensing Spine), you
 * may not (a) modify, translate, adapt or otherwise create derivative works,
 * improvements of the Software or develop new applications using the Software
 * or (b) remove, delete, alter or obscure any trademarks or any copyright,
 * trademark, patent or other intellectual property or proprietary rights
 * notices on or in the Software, including any copy thereof. Redistributions
 * in binary or source form must include this license and terms.
 * 
 * THIS SOFTWARE IS PROVIDED BY ESOTERIC SOFTWARE "AS IS" AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO
 * EVENT SHALL ESOTERIC SOFTARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
 * OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
 * OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *****************************************************************************/

using System;
using System.Collections.Generic;

namespace Spine {
	/// <summary>Stores attachments by slot index and attachment name.</summary>
	public class Skin {
		internal String name;

		// HuaHua. quickly find
		private Dictionary<int, Dictionary<string, Attachment>> attachments =
			new Dictionary<int, Dictionary<string, Attachment>>();

#if UNITY_EDITOR
		//HuaHua
		internal Dictionary<int, Dictionary<string, Attachment>> Attachments
        {
            get
            {
				return attachments;
			}
        }
#endif

		public String Name { get { return name; } }

		public Skin (String name) {
			if (name == null) throw new ArgumentNullException("name cannot be null.");
			this.name = name;
		}

		public void AddAttachment (int slotIndex, String name, Attachment attachment) {
			if (attachment == null) throw new ArgumentNullException("attachment cannot be null.");

			if (!attachments.TryGetValue(slotIndex, out Dictionary<string, Attachment> ats))
            {
				ats = new Dictionary<string, Attachment>();
				attachments.Add(slotIndex, ats);
			}
			ats[name] = attachment;
		}

		/// <returns>May be null.</returns>
		public Attachment GetAttachment (int slotIndex, String name) {
            if (!attachments.TryGetValue(slotIndex, out Dictionary<string, Attachment> ats))
            {
				return null;
            }

			ats.TryGetValue(name, out Attachment attachment);
            return attachment;
		}

		public void RemoveAttachment(int slotIndex, string name) {
            if (attachments.TryGetValue(slotIndex, out Dictionary<string, Attachment> ats))
            {
				ats.Remove(name);
				if (ats.Count == 0)
                {
					attachments.Remove(slotIndex);
				}
			}
        }

		public void RemoveAttachmentsForSlot(int slotIndex)
        {
			var names = new List<string>();
			FindNamesForSlot(slotIndex, names);
            foreach (var name in names)
            {
				RemoveAttachment(slotIndex, name);
            }
        }

		public void FindNamesForSlot (int slotIndex, List<String> names) {
			if (names == null) throw new ArgumentNullException("names cannot be null.");
			if (attachments.TryGetValue(slotIndex, out Dictionary<string, Attachment> ats))
			{
				foreach(var each in ats)
                {
					names.Add(each.Key);
				}
			}
		}

		public void FindAttachmentsForSlot (int slotIndex, List<Attachment> attachments) {
			if (attachments == null) throw new ArgumentNullException("attachments cannot be null.");
            if (this.attachments.TryGetValue(slotIndex, out Dictionary<string, Attachment> ats))
            {
                foreach (var each in ats)
                {
					attachments.Add(each.Value);
                }
            }
		}

		override public String ToString () {
			return name;
		}

		///<summary>Adds all attachments from the specified skin to this skin.</summary>
		public void CopySkin(Skin skin)
		{
			foreach (var entry in skin.attachments)
			{
				int slotIndex = entry.Key;
				foreach(var each in entry.Value)
                {
					AddAttachment(slotIndex, each.Key, each.Value);
				}
			}
		}

		public void DetachSkin(Skin skin)
        {
			foreach (var entry in skin.attachments)
			{
                int slotIndex = entry.Key;
				foreach (var each in entry.Value)
				{
					RemoveAttachment(slotIndex, each.Key);
				}
			}
		}

		// Avoids boxing in the dictionary.
		private class AttachmentComparer : IEqualityComparer<KeyValuePair<int, String>> {
			internal static readonly AttachmentComparer Instance = new AttachmentComparer();

			bool IEqualityComparer<KeyValuePair<int, string>>.Equals (KeyValuePair<int, string> o1, KeyValuePair<int, string> o2) {
				return o1.Key == o2.Key && o1.Value == o2.Value;
			}

			int IEqualityComparer<KeyValuePair<int, string>>.GetHashCode (KeyValuePair<int, string> o) {
				return o.Key;
			}
		}
	}
}
