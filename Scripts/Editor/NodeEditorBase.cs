﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XNodeEditor.Internal {
	/// <summary> Handles caching of custom editor classes and their target types. Accessible with GetEditor(Type type) </summary>
	/// <typeparam name="T">Editor Type. Should be the type of the deriving script itself (eg. NodeEditor) </typeparam>
	/// <typeparam name="A">Attribute Type. The attribute used to connect with the runtime type (eg. CustomNodeEditorAttribute) </typeparam>
	/// <typeparam name="K">Runtime Type. The Object this can be an editor for (eg. INode ) </typeparam>
	public abstract class NodeEditorBase<T, A, K> : Editor where T : NodeEditorBase<T, A, K>, ICustomEditor<K> where A : Attribute, INodeEditorAttrib where K : class {
		/// <summary> Custom editors defined with [CustomNodeEditor] </summary>
		private static Dictionary<Type, Type> editorTypes;
		private static Dictionary<K, T> editors = new Dictionary<K, T>();
		public NodeEditorWindow window;
		public new K target { get { return _target as UnityEngine.Object == base.target ? _target : _target = base.target as K; } set { base.target = value as UnityEngine.Object; } }
		private K _target;

		public static T GetEditor<Q>(Q target, NodeEditorWindow window) where Q : class {
			if ((target as UnityEngine.Object) == null) return default(T);
			T editor;
			if (!editors.TryGetValue(target as K, out editor)) {
				Type type = target.GetType();
				Type editorType = GetEditorType(type);
				editor = (T) Editor.CreateEditor(target as UnityEngine.Object, editorType);
				editor.window = window;
				editors.Add(target as K, editor);
			}
			if (editor.target == null) editor.Initialize(new UnityEngine.Object[] { target as UnityEngine.Object });
			if (editor.window != window) editor.window = window;
			return editor;
		}

		private static Type GetEditorType(Type type) {
			if (type == null) return null;
			if (editorTypes == null) CacheCustomEditors();
			Type result;
			if (editorTypes.TryGetValue(type, out result)) return result;
			//If type isn't found, try base type
			return GetEditorType(type.BaseType);
		}

		private static void CacheCustomEditors() {
			editorTypes = new Dictionary<Type, Type>();

			//Get all classes deriving from NodeEditor via reflection
			Type[] nodeEditors = XNodeEditor.NodeEditorWindow.GetDerivedTypes(typeof(T));
			for (int i = 0; i < nodeEditors.Length; i++) {
				if (nodeEditors[i].IsAbstract) continue;
				var attribs = nodeEditors[i].GetCustomAttributes(typeof(A), false);
				if (attribs == null || attribs.Length == 0) continue;
				A attrib = attribs[0] as A;
				editorTypes.Add(attrib.GetInspectedType(), nodeEditors[i]);
			}
		}
	}

	public interface INodeEditorAttrib {
		Type GetInspectedType();
	}
}