﻿﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using XNode;

namespace XNodeEditor {
    /// <summary> xNode-specific version of <see cref="EditorGUILayout"/> </summary>
    public static class NodeEditorGUILayout {

        private static readonly Dictionary<UnityEngine.Object, Dictionary<string, ReorderableList>> reorderableListCache = new Dictionary<UnityEngine.Object, Dictionary<string, ReorderableList>>();
        private static int reorderableListIndex = -1;

        /// <summary> Make a field for a serialized property. Automatically displays relevant node port. </summary>
        public static void PropertyField(SerializedProperty property, bool includeChildren = true, params GUILayoutOption[] options) {
            PropertyField(property, (GUIContent) null, includeChildren, options);
        }

        /// <summary> Make a field for a serialized property. Automatically displays relevant node port. </summary>
        public static void PropertyField(SerializedProperty property, GUIContent label, bool includeChildren = true, params GUILayoutOption[] options) {
            if (property == null) throw new NullReferenceException();
            XNode.Node node = property.serializedObject.targetObject as XNode.Node;
            XNode.NodePort port = node.GetPort(property.name);
            PropertyField(property, label, port, includeChildren);
        }

        /// <summary> Make a field for a serialized property. Manual node port override. </summary>
        public static void PropertyField(SerializedProperty property, XNode.NodePort port, bool includeChildren = true, params GUILayoutOption[] options) {
            PropertyField(property, null, port, includeChildren, options);
        }

        /// <summary> Make a field for a serialized property. Manual node port override. </summary>
        public static void PropertyField(SerializedProperty property, GUIContent label, XNode.NodePort port, bool includeChildren = true, params GUILayoutOption[] options) {
            if (property == null) throw new NullReferenceException();
            
            // If property is not a port, display a regular property field
            if (port == null) EditorGUILayout.PropertyField(property, label, includeChildren, GUILayout.MinWidth(30));
            else {
            
                Node.LabelAttribute labelAttribute;
            
                if (NodeEditorUtilities.GetCachedAttrib(port.node.GetType(), property.name, out labelAttribute))
                {
                    if (label != null)
                    {
                        label.text = labelAttribute.Label;
                    }
                    else
                    {
                        label = new GUIContent(labelAttribute.Label);
                    }
                }
                
                Rect rect = new Rect();

                List<PropertyAttribute> propertyAttributes = NodeEditorUtilities.GetCachedPropertyAttribs(port.node.GetType(), property.name);

                // If property is an input, display a regular property field and put a port handle on the left side
                if (port.direction == XNode.NodePort.IO.Input) {
                    // Get data from [Input] attribute
                    XNode.Node.ShowBackingValue showBacking = XNode.Node.ShowBackingValue.Unconnected;
                    XNode.Node.InputAttribute inputAttribute;
                    bool dynamicPortList = false;
                    if (NodeEditorUtilities.GetCachedAttrib(port.node.GetType(), property.name, out inputAttribute)) {
                        dynamicPortList = inputAttribute.dynamicPortList;
                        showBacking = inputAttribute.backingValue;
                    }

                    bool usePropertyAttributes = dynamicPortList ||
                        showBacking == XNode.Node.ShowBackingValue.Never ||
                        (showBacking == XNode.Node.ShowBackingValue.Unconnected && port.IsConnected);

                    float spacePadding = 0;
                    foreach (var attr in propertyAttributes) {
                        if (attr is SpaceAttribute) {
                            if (usePropertyAttributes) GUILayout.Space((attr as SpaceAttribute).height);
                            else spacePadding += (attr as SpaceAttribute).height;
                        } else if (attr is HeaderAttribute) {
                            if (usePropertyAttributes) {
                                //GUI Values are from https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/ScriptAttributeGUI/Implementations/DecoratorDrawers.cs
                                Rect position = GUILayoutUtility.GetRect(0, (EditorGUIUtility.singleLineHeight * 1.5f) - EditorGUIUtility.standardVerticalSpacing); //Layout adds standardVerticalSpacing after rect so we subtract it.
                                position.yMin += EditorGUIUtility.singleLineHeight * 0.5f;
                                position = EditorGUI.IndentedRect(position);
                                GUI.Label(position, (attr as HeaderAttribute).header, EditorStyles.boldLabel);
                            } else spacePadding += EditorGUIUtility.singleLineHeight * 1.5f;
                        }
                    }

                    if (dynamicPortList) {
                        Type type = GetType(property);
                        XNode.Node.ConnectionType connectionType = inputAttribute != null ? inputAttribute.connectionType : XNode.Node.ConnectionType.Multiple;
                        DynamicPortList(property.name, type, property.serializedObject, port.direction, connectionType);
                        return;
                    }
                    switch (showBacking) {
                        case XNode.Node.ShowBackingValue.Unconnected:
                            // Display a label if port is connected
                            if (port.IsConnected) EditorGUILayout.LabelField(label != null ? label : new GUIContent(property.displayName));
                            // Display an editable property field if port is not connected
                            else EditorGUILayout.PropertyField(property, label, includeChildren, GUILayout.MinWidth(30));
                            break;
                        case XNode.Node.ShowBackingValue.Never:
                            // Display a label
                            EditorGUILayout.LabelField(label != null ? label : new GUIContent(property.displayName));
                            break;
                        case XNode.Node.ShowBackingValue.Always:
                            // Display an editable property field
                            EditorGUILayout.PropertyField(property, label, includeChildren, GUILayout.MinWidth(30));
                            break;
                    }

                    rect = GUILayoutUtility.GetLastRect();
                    rect.position = rect.position - new Vector2(16, -spacePadding);
                    // If property is an output, display a text label and put a port handle on the right side
                } else if (port.direction == XNode.NodePort.IO.Output) {
                    // Get data from [Output] attribute
                    XNode.Node.ShowBackingValue showBacking = XNode.Node.ShowBackingValue.Unconnected;
                    XNode.Node.OutputAttribute outputAttribute;
                    bool dynamicPortList = false;
                    if (NodeEditorUtilities.GetCachedAttrib(port.node.GetType(), property.name, out outputAttribute)) {
                        dynamicPortList = outputAttribute.dynamicPortList;
                        showBacking = outputAttribute.backingValue;
                    }

                    bool usePropertyAttributes = dynamicPortList ||
                        showBacking == XNode.Node.ShowBackingValue.Never ||
                        (showBacking == XNode.Node.ShowBackingValue.Unconnected && port.IsConnected);

                    float spacePadding = 0;
                    foreach (var attr in propertyAttributes) {
                        if (attr is SpaceAttribute) {
                            if (usePropertyAttributes) GUILayout.Space((attr as SpaceAttribute).height);
                            else spacePadding += (attr as SpaceAttribute).height;
                        } else if (attr is HeaderAttribute) {
                            if (usePropertyAttributes) {
                                //GUI Values are from https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/ScriptAttributeGUI/Implementations/DecoratorDrawers.cs
                                Rect position = GUILayoutUtility.GetRect(0, (EditorGUIUtility.singleLineHeight * 1.5f) - EditorGUIUtility.standardVerticalSpacing); //Layout adds standardVerticalSpacing after rect so we subtract it.
                                position.yMin += EditorGUIUtility.singleLineHeight * 0.5f;
                                position = EditorGUI.IndentedRect(position);
                                GUI.Label(position, (attr as HeaderAttribute).header, EditorStyles.boldLabel);
                            } else spacePadding += EditorGUIUtility.singleLineHeight * 1.5f;
                        }
                    }

                    if (dynamicPortList) {
                        Type type = GetType(property);
                        XNode.Node.ConnectionType connectionType = outputAttribute != null ? outputAttribute.connectionType : XNode.Node.ConnectionType.Multiple;
                        DynamicPortList(property.name, type, property.serializedObject, port.direction, connectionType);
                        return;
                    }
                    switch (showBacking) {
                        case XNode.Node.ShowBackingValue.Unconnected:
                            // Display a label if port is connected
                            if (port.IsConnected) EditorGUILayout.LabelField(label != null ? label : new GUIContent(property.displayName), NodeEditorResources.OutputPort, GUILayout.MinWidth(30));
                            // Display an editable property field if port is not connected
                            else EditorGUILayout.PropertyField(property, label, includeChildren, GUILayout.MinWidth(30));
                            break;
                        case XNode.Node.ShowBackingValue.Never:
                            // Display a label
                            EditorGUILayout.LabelField(label != null ? label : new GUIContent(property.displayName), NodeEditorResources.OutputPort, GUILayout.MinWidth(30));
                            break;
                        case XNode.Node.ShowBackingValue.Always:
                            // Display an editable property field
                            EditorGUILayout.PropertyField(property, label, includeChildren, GUILayout.MinWidth(30));
                            break;
                    }

                    rect = GUILayoutUtility.GetLastRect();
                    rect.position = rect.position + new Vector2(rect.width, spacePadding);
                }

                rect.size = new Vector2(16, 16);

                NodeEditor editor = NodeEditor.GetEditor(port.node, NodeEditorWindow.current);
                Color backgroundColor = editor.GetTint();
                Color col = NodeEditorWindow.current.graphEditor.GetPortColor(port);
                DrawPortHandle(rect, backgroundColor, col);

                // Register the handle position
                Vector2 portPos = rect.center;
                NodeEditor.portPositions[port] = portPos;
            }
        }

        private static System.Type GetType(SerializedProperty property) {
            System.Type parentType = property.serializedObject.targetObject.GetType();
            System.Reflection.FieldInfo fi = parentType.GetFieldInfo(property.name);
            return fi.FieldType;
        }

        /// <summary> Make a simple port field. </summary>
        public static void PortField(XNode.NodePort port, params GUILayoutOption[] options) {
            PortField(null, port, options);
        }

        /// <summary> Make a simple port field. </summary>
        public static void PortField(GUIContent label, XNode.NodePort port, params GUILayoutOption[] options) {
            if (port == null) return;
            if (options == null) options = new GUILayoutOption[] { GUILayout.MinWidth(30) };
            Vector2 position = Vector3.zero;
            GUIContent content = label != null ? label : new GUIContent(ObjectNames.NicifyVariableName(port.fieldName));

            // If property is an input, display a regular property field and put a port handle on the left side
            if (port.direction == XNode.NodePort.IO.Input) {
                // Display a label
                EditorGUILayout.LabelField(content, options);

                Rect rect = GUILayoutUtility.GetLastRect();
                position = rect.position - new Vector2(16, 0);
            }
            // If property is an output, display a text label and put a port handle on the right side
            else if (port.direction == XNode.NodePort.IO.Output) {
                // Display a label
                EditorGUILayout.LabelField(content, NodeEditorResources.OutputPort, options);

                Rect rect = GUILayoutUtility.GetLastRect();
                position = rect.position + new Vector2(rect.width, 0);
            }
            PortField(position, port);
        }

        /// <summary> Make a simple port field. </summary>
        public static void PortField(Vector2 position, XNode.NodePort port) {
            if (port == null) return;

            Rect rect = new Rect(position, new Vector2(16, 16));

            NodeEditor editor = NodeEditor.GetEditor(port.node, NodeEditorWindow.current);
            Color backgroundColor = editor.GetTint();
            Color col = NodeEditorWindow.current.graphEditor.GetPortColor(port);
            DrawPortHandle(rect, backgroundColor, col);
            
            //当选择节点时显示所有的输入点索引
            if (port.direction == XNode.NodePort.IO.Output)
            {
                if (port.Connection != null && port.Connection.ConnectionCount > 1)
                {
                    if (port.Connection.node == Selection.activeObject)
                    {
                        var dCol = GUI.color;
                        var fontStyle = EditorStyles.label.fontStyle;
                        var textCol = Color.white - col;
                        textCol.a = 1;
                        var index = port.Connection.GetConnectionIndex(port);
                        EditorStyles.label.fontStyle = FontStyle.Bold;
                        GUI.contentColor = textCol;
                        {
                            EditorGUI.LabelField(new Rect( rect.position + new Vector2(rect.size.x / 4 - 0.5f,0), rect.size),
                                (index + 1).ToString());
                        }
                        GUI.contentColor = dCol;
                        EditorStyles.label.fontStyle = fontStyle;
                    } 
                }
            }
            
            // Register the handle position
            Vector2 portPos = rect.center;
            NodeEditor.portPositions[port] = portPos;
        }

        /// <summary> Add a port field to previous layout element. </summary>
        public static void AddPortField(XNode.NodePort port) {
            if (port == null) return;
            Rect rect = new Rect();

            // If property is an input, display a regular property field and put a port handle on the left side
            if (port.direction == XNode.NodePort.IO.Input) {
                rect = GUILayoutUtility.GetLastRect();
                rect.position = rect.position - new Vector2(16, 0);
                // If property is an output, display a text label and put a port handle on the right side
            } else if (port.direction == XNode.NodePort.IO.Output) {
                rect = GUILayoutUtility.GetLastRect();
                rect.position = rect.position + new Vector2(rect.width, 0);
            }

            rect.size = new Vector2(16, 16);

            NodeEditor editor = NodeEditor.GetEditor(port.node, NodeEditorWindow.current);
            Color backgroundColor = editor.GetTint();
            Color col = NodeEditorWindow.current.graphEditor.GetPortColor(port);
            DrawPortHandle(rect, backgroundColor, col);

            // Register the handle position
            Vector2 portPos = rect.center;
            NodeEditor.portPositions[port] = portPos;
        }

        /// <summary> Draws an input and an output port on the same line </summary>
        public static void PortPair(XNode.NodePort input, XNode.NodePort output) {
            GUILayout.BeginHorizontal();
            NodeEditorGUILayout.PortField(input, GUILayout.MinWidth(0));
            NodeEditorGUILayout.PortField(output, GUILayout.MinWidth(0));
            GUILayout.EndHorizontal();
        }

        public static void DrawPortHandle(Rect rect, Color backgroundColor, Color typeColor) {
            Color col = GUI.color;
            GUI.color = backgroundColor;
            GUI.DrawTexture(rect, NodeEditorResources.dotOuter);
            GUI.color = typeColor;
            GUI.DrawTexture(rect, NodeEditorResources.dot);
            GUI.color = col;
        }

#region Obsolete
        [Obsolete("Use IsDynamicPortListPort instead")]
        public static bool IsInstancePortListPort(XNode.NodePort port) {
            return IsDynamicPortListPort(port);
        }

        [Obsolete("Use DynamicPortList instead")]
        public static void InstancePortList(string fieldName, Type type, SerializedObject serializedObject, XNode.NodePort.IO io, XNode.Node.ConnectionType connectionType = XNode.Node.ConnectionType.Multiple, XNode.Node.TypeConstraint typeConstraint = XNode.Node.TypeConstraint.None, Action<ReorderableList> onCreation = null) {
            DynamicPortList(fieldName, type, serializedObject, io, connectionType, typeConstraint, onCreation);
        }
#endregion

        /// <summary> Is this port part of a DynamicPortList? </summary>
        public static bool IsDynamicPortListPort(XNode.NodePort port) {
            string[] parts = port.fieldName.Split(' ');
            if (parts.Length != 2) return false;
            Dictionary<string, ReorderableList> cache;
            if (reorderableListCache.TryGetValue(port.node, out cache)) {
                ReorderableList list;
                if (cache.TryGetValue(parts[0], out list)) return true;
            }
            return false;
        }

        /// <summary> Draw an editable list of dynamic ports.</summary>
        /// <param name="fieldName">Supply a list for editable values</param>
        /// <param name="type">Value type of added dynamic ports</param>
        /// <param name="serializedObject">The serializedObject of the node</param>
        /// <param name="connectionType">Connection type of added dynamic ports</param>
        /// <param name="onCreation">Called on the list on creation. Use this if you want to customize the created ReorderableList</param>
        /// <param name="onAdd">Return port name after adding port</param>
        public static void DynamicPortList(string fieldName, Type type, SerializedObject serializedObject, XNode.NodePort.IO io,
            XNode.Node.ConnectionType connectionType = XNode.Node.ConnectionType.Multiple,
            XNode.Node.TypeConstraint typeConstraint = XNode.Node.TypeConstraint.None,
            Action<ReorderableList> onCreation = null,Action<string> onAdd = null) {
            XNode.Node node = serializedObject.targetObject as XNode.Node;

            var indexedPorts = io == NodePort.IO.Input ? node.DynamicInputs : node.DynamicOutputs;
            
            List<XNode.NodePort> dynamicPorts = indexedPorts.ToList();

            ReorderableList list = null;
            Dictionary<string, ReorderableList> rlc;
            //todo rename no update cache -- 2019年11月28日03点00分
            if (reorderableListCache.TryGetValue(serializedObject.targetObject, out rlc)) {
                if (!rlc.TryGetValue(fieldName, out list)) list = null;
            }
            // If a ReorderableList isn't cached for this array, do so.
            if (list == null) {
                SerializedProperty arrayData = serializedObject.FindProperty(fieldName);
                list = CreateReorderableList(fieldName, dynamicPorts, arrayData, type, serializedObject, 
                    io, connectionType, typeConstraint, onAdd);
                onCreation?.Invoke(list);
                if (reorderableListCache.TryGetValue(serializedObject.targetObject, out rlc)) rlc.Add(fieldName, list);
                else reorderableListCache.Add(serializedObject.targetObject, new Dictionary<string, ReorderableList>() { { fieldName, list } });
            }
            list.list = dynamicPorts;
            list.DoLayoutList();
        }

        private static ReorderableList CreateReorderableList(string fieldName, List<XNode.NodePort> dynamicPorts, SerializedProperty arrayData, Type type, 
            SerializedObject serializedObject, XNode.NodePort.IO io, XNode.Node.ConnectionType connectionType, XNode.Node.TypeConstraint typeConstraint,Action<string> onAdd) {
            bool hasArrayData = arrayData != null && arrayData.isArray;
            XNode.Node node = serializedObject.targetObject as XNode.Node;
            ReorderableList list = new ReorderableList(dynamicPorts, null, true, true, true, true);
            string label = arrayData != null ? arrayData.displayName : ObjectNames.NicifyVariableName(fieldName);

            list.drawElementCallback =
                (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    XNode.NodePort port = (NodePort) list.list[index];
                    if (hasArrayData) 
                    {
                        if (arrayData.arraySize <= index) {
                            EditorGUI.LabelField(rect, "Array[" + index + "] data out of range");
                            return;
                        }
                        SerializedProperty itemData = arrayData.GetArrayElementAtIndex(index);
                        EditorGUI.PropertyField(rect, itemData, true);
                    } 
                    else 
                        EditorGUI.LabelField(rect, port != null ? port.fieldName : "");
                    
                    if (port != null) {
                        Vector2 pos = rect.position + (port.IsOutput?new Vector2(rect.width + 6, 0) : new Vector2(-36, 0));
                        NodeEditorGUILayout.PortField(pos, port);
                    }
                };
            list.elementHeightCallback =
                (int index) => {
                    if (hasArrayData) {
                        if (arrayData.arraySize <= index) return EditorGUIUtility.singleLineHeight;
                        SerializedProperty itemData = arrayData.GetArrayElementAtIndex(index);
                        return EditorGUI.GetPropertyHeight(itemData);
                    } else return EditorGUIUtility.singleLineHeight;
                };
            
            list.drawHeaderCallback =
                (Rect rect) => {
                    EditorGUI.LabelField(rect, label);
                };
            
            list.onSelectCallback =
                (ReorderableList rl) => {
                    reorderableListIndex = rl.index;
                };

            list.onReorderCallbackWithDetails = (reorderableList, index, newIndex) =>
            {
                var portSer = serializedObject.FindProperty(Node.PortFieldName);
                SerializedProperty keys = portSer.FindPropertyRelative(Node.KeysFieldName);
                SerializedProperty values = portSer.FindPropertyRelative(Node.ValuesFieldName);

                var baseIndex = 0;

                if (io == NodePort.IO.Output)
                {
                    foreach (var nodePort in node.Ports)
                    {
                        if (nodePort.direction == NodePort.IO.Input)
                        {
                            baseIndex++;
                        
                            continue;
                        }
                    
                        break;
                    }

                    baseIndex++;
                }

                index += baseIndex;
                newIndex += baseIndex;
                keys.MoveArrayElement(index, newIndex);
                values.MoveArrayElement(index, newIndex);
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(node);
                serializedObject.Update();

                foreach (NodePort port in reorderableList.list)
                {
                    port.RefreshValueType();
                }
               
                NodeEditorWindow.current.Repaint();
                EditorApplication.delayCall += NodeEditorWindow.current.Repaint;
            };

            list.onAddCallback =
                (ReorderableList rl) => {
                    // Add dynamic port postfixed with an index number
                    string newName = fieldName + " 0";
                    int i = 0;
                    while (node.HasPort(newName)) newName = fieldName + " " + (++i);

                    if (io == XNode.NodePort.IO.Output) node.AddDynamicOutput(type, connectionType, XNode.Node.TypeConstraint.None, newName);
                    else node.AddDynamicInput(type, connectionType, typeConstraint, newName);
                    serializedObject.Update();
                    EditorUtility.SetDirty(node);
                    if (hasArrayData) {
                        arrayData.InsertArrayElementAtIndex(arrayData.arraySize);
                    }
                    serializedObject.ApplyModifiedProperties();
                    
                    onAdd?.Invoke(newName);
                };
            list.onRemoveCallback =
                (ReorderableList rl) =>
                {
                    int index = rl.index;

                    NodePort o = (NodePort) rl.list[index];
                    
                    node.RemoveDynamicPort(o);
                 
                    EditorUtility.SetDirty(node);
                };

            if (hasArrayData) {
                int dynamicPortCount = dynamicPorts.Count;
                while (dynamicPortCount < arrayData.arraySize) {
                    // Add dynamic port postfixed with an index number
                    string newName = arrayData.name + " 0";
                    int i = 0;
                    while (node.HasPort(newName)) newName = arrayData.name + " " + (++i);
                    if (io == XNode.NodePort.IO.Output) node.AddDynamicOutput(type, connectionType, typeConstraint,newName);
                    else node.AddDynamicInput(type, connectionType, typeConstraint, newName);
                    EditorUtility.SetDirty(node);
                    dynamicPortCount++;
                }
                while (arrayData.arraySize < dynamicPortCount) {
                    arrayData.InsertArrayElementAtIndex(arrayData.arraySize);
                }
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }

            return list;
        }
    }
}
