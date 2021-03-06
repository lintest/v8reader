﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using V8Reader.Core;

namespace V8Reader.Comparison
{

    interface IComparableItem
    {
        bool CompareTo(object Comparand);
        IDiffViewer GetDifferenceViewer(object Comparand);
    }

    interface IComparator
    {
        bool CompareObjects(object Compared, object Comparand);
    }

    interface IDiffViewer
    {
        void ShowDifference();
        void ShowDifference(string NameCurrent, string NameComparand);
    }

    class ComparisonPerformer : IComparisonPerformer
    {
        public ComparisonPerformer(IMDTreeItem Left, IMDTreeItem Right)
        {
            m_Left = Left;
            m_Right = Right;
        }

        public ComparisonResult Perform()
        {
            return Perform(MatchingMode.ByName);
        }

        public ComparisonResult Perform(MatchingMode Mode)
        {
            ComparisonResult result = new ComparisonResult();
            m_CurrentMode = Mode;

            FillComparisonNode(m_Left, m_Right, result);

            return result;

        }

        private void FillComparisonNode(IMDTreeItem Left, IMDTreeItem Right, ComparisonItem node)
        {
            if ((Left != null && Right != null) && Left.GetType() != Right.GetType())
            {
                throw new InvalidOperationException("Compared components must be of the same type");
            }

            FillSide(node.Left, Left);
            FillSide(node.Right, Right);

            if (Left == null)
            {
                TraverseRightObject(Right, node);
                return;
            }

            if (Left is IMDPropertyProvider)
            {
                CompareProperties((IMDPropertyProvider)Left, (IMDPropertyProvider)Right, node);
            }

            if (Left is IComparableItem)
            {
                // Разница определяется самим объектом
                node.IsDiffer = !((IComparableItem)Left).CompareTo(Right);
            }
            else if (Left is MDObjectsCollection<MDObjectBase> || Left is StaticTreeNode)
            {
                MapAndCompareCollections(Left, Right, node);
            }
            else if( Left.HasChildren() )
            {
                // это статичные свойства метаданных
                // порядок свойств будет соблюдаться в обоих объектах
                var LeftWalker = Left.ChildItems.GetEnumerator();

                IEnumerator<IMDTreeItem> RightWalker = null;
                if (Right != null)
                {
                    RightWalker = Right.ChildItems.GetEnumerator();
                }

                while (LeftWalker.MoveNext())
                {
                    var newNode = new ComparisonItem();

                    if (Right != null)
                    {
                        RightWalker.MoveNext();
                        FillComparisonNode(LeftWalker.Current, RightWalker.Current, newNode);
                    }
                    else
                    {
                        FillComparisonNode(LeftWalker.Current, null, newNode);
                    }

                    if (!(newNode.Left == null && newNode.Right == null))
                    {
                        node.Items.Add(newNode);
                        node.IsDiffer = node.IsDiffer || newNode.IsDiffer;
                    }
                        
                }

            }

        }

        private void MapAndCompareCollections(IMDTreeItem Left, IMDTreeItem Right, ComparisonItem node)
        {
            IEnumerable<IMDTreeItem> LeftItems;
            IEnumerable<IMDTreeItem> RightItems;
            if (m_CurrentMode == MatchingMode.ByID)
            {
                LeftItems = from item in Left.ChildItems orderby item.Key, item.Text select item;
                RightItems = from item in Right.ChildItems orderby item.Key, item.Text select item;
            }
            else
            {
                LeftItems = from item in Left.ChildItems orderby item.Text, item.Text select item;
                RightItems = from item in Right.ChildItems orderby item.Text, item.Text select item;
            }

            var LeftList = LeftItems.ToArray<IMDTreeItem>();
            var RightList = RightItems.ToArray<IMDTreeItem>();

            int leftIdx = 0;
            int rightIdx = 0;
            int leftCount = LeftList.Length - 1;
            int rightCount = RightList.Length - 1;

            bool Modified = false;

            while (true)
            {
                if (leftIdx > leftCount)
                {
                    rightIdx++;
                    if (rightIdx > rightCount)
                    {
                        break;
                    }

                    var Item = RightList[rightIdx];
                    AddAndFillNewNode(null, Item, node);
                    Modified = true;
                    continue;

                }

                if (rightIdx > rightCount)
                {
                    leftIdx++;
                    if (leftIdx > leftCount)
                    {
                        break;
                    }

                    var Item = LeftList[leftIdx];
                    AddAndFillNewNode(Item, null, node);
                    Modified = true;
                    continue;

                }

                var LeftItem = LeftList[leftIdx];
                var RightItem = RightList[rightIdx];

                int? comparisonResult = CompareObjects(LeftItem, RightItem);

                if (comparisonResult == 0)
                {
                    var addedNode = AddAndFillNewNode(LeftItem, RightItem, node);
                    if (addedNode != null)
                    {
                        Modified = Modified || addedNode.IsDiffer;
                    }

                    leftIdx++;
                    rightIdx++;

                }
                else if (comparisonResult < 0)
                {
                    AddAndFillNewNode(LeftItem, null, node);
                    Modified = true;
                    leftIdx++;
                }
                else if (comparisonResult > 0)
                {
                    AddAndFillNewNode(null, RightItem, node);
                    Modified = true;
                    rightIdx++;
                }
                else
                {
                    AddAndFillNewNode(LeftItem, null, node);
                    AddAndFillNewNode(null, RightItem, node);

                    leftIdx++;
                    rightIdx++;

                    Modified = true;

                }

            }

            if (Modified)
            {
                node.IsDiffer = true;
            }
        }

        private int? CompareObjects(IMDTreeItem left, IMDTreeItem right)
        {
            if (left.GetType() == right.GetType())
            {
                if (m_CurrentMode == MatchingMode.ByID)
                {
                    return String.CompareOrdinal(left.Key, right.Key);
                }
                else
                {
                    return String.CompareOrdinal(left.Text, right.Text);
                }
            }
            else
            {
                return null;
            }
        }

        private ComparisonItem AddAndFillNewNode(IMDTreeItem Left, IMDTreeItem Right, ComparisonItem ParentNode)
        {
            ComparisonItem newNode = new ComparisonItem();
            FillComparisonNode(Left, Right, newNode);

            if (!(newNode.Left == null && newNode.Right == null))
            {
                ParentNode.Items.Add(newNode);
                return newNode;
            }
            else
            {
                return null;
            }
        }

        private void TraverseRightObject(IMDTreeItem Right, ComparisonItem ParentNode)
        {
            if (Right.HasChildren())
            {
                foreach (var item in Right.ChildItems)
                {
                    AddAndFillNewNode(null, item, ParentNode);
                }

            }

            if (Right is IMDPropertyProvider)
            {
                var PropStub = ParentNode.AddStaticNode("Свойства");
                var PropProv = Right as IMDPropertyProvider;

                foreach (PropDef propDef in PropProv.Properties.Values)
                {
                    var propNode = new ComparisonItem(null, propDef.Value, propDef.Name);
                    propNode.IsDiffer = true;

                    PropStub.Items.Add(propNode);

                }

                PropStub.IsDiffer = true;

            }

        }

        private void FillSide(ComparisonSide SideObject, IMDTreeItem Value)
        {
            SideObject.Object = Value;
            if (Value == null)
            {
                SideObject.Presentation = "<отсутствует>";
            }
            else
            {
                SideObject.Presentation = Value.Text;
            }
        }

        private ComparisonItem CompareProperties(IMDPropertyProvider Left, IMDPropertyProvider Right, ComparisonItem parentNode)
        {
            var PropStub = parentNode.AddStaticNode("Свойства");
            
            bool HasDifference = false;
            
            foreach (PropDef propDef in Left.Properties.Values)
            {
                if (m_CurrentMode == MatchingMode.ByName && propDef.Key == "ID")
                {
                    continue;
                }

                object RightVal = null;
                PropDef RightProperty = null;

                if (Right != null)
                {
                    RightVal = Right.GetValue(propDef.Key);
                    RightProperty = Right.Properties[propDef.Key];
                }

                bool childDifference = !propDef.CompareTo(RightVal);
                HasDifference = HasDifference || childDifference;

                var newNode = new ComparisonItem(propDef, RightProperty, propDef.Name);
                newNode.IsDiffer = childDifference;

                PropStub.Items.Add(newNode);

            }

            parentNode.IsDiffer = HasDifference;
            PropStub.IsDiffer = HasDifference;
            
            return PropStub;

        }

        public enum MatchingMode
        {
            ByID,
            ByName
        }

        private IMDTreeItem m_Left;
        private IMDTreeItem m_Right;
        private MatchingMode m_CurrentMode;

    }

    class ComparisonItem
    {

        public ComparisonItem()
        {
            Left = new ComparisonSide();
            Right = new ComparisonSide();
            m_Items = new ItemsCollection(this);
        }

        public ComparisonItem(object left, object right, string name)
        {
            Left = new ComparisonSide() { Object = left, Presentation = name };
            Right = new ComparisonSide { Object = right, Presentation = name };
            m_Items = new ItemsCollection(this);
        }

        public ComparisonSide Left { get; private set; }
        public ComparisonSide Right { get; private set; }
        public IList<ComparisonItem> Items { get { return m_Items; } }
        public ComparisonItem Parent { get; private set; }
        public ResultNodeType NodeType 
        {
            get
            {
                var checkSide = Left.Object == null ? Right : Left;
                if (checkSide == null)
                {
                    return ResultNodeType.FakeNode;
                }
                else
                {
                    if (checkSide.Object is MDObjectsCollection<MDObjectBase> || checkSide.Object is StaticTreeNode)
                    {
                        return ResultNodeType.ObjectsCollection;
                    }
                    else if (checkSide.Object is MDObjectBase)
                    {
                        return ResultNodeType.Object;
                    }
                    else if (checkSide.Object is PropDef)
                    {
                        return ResultNodeType.PropertyDef;
                    }
                    else
                    {
                        return ResultNodeType.FakeNode;
                    }
                }
            }
        }

        public ComparisonStatus Status
        {
            get
            {
                if (Left.Object == null && Right.Object != null)
                {
                    return ComparisonStatus.Added;
                }
                else if (Left.Object != null && Right.Object == null)
                {
                    return ComparisonStatus.Deleted;
                }
                else if (IsDiffer)
                {
                    return ComparisonStatus.Modified;
                }
                else
                {
                    return ComparisonStatus.Match;
                }
            }
        }

        public bool IsDiffer { get; set; }

        public ComparisonItem AddStaticNode(string name)
        {
            var newNode = new ComparisonItem();
            newNode.Left.Presentation = name;
            newNode.Right.Presentation = name;
            this.Items.Add(newNode);

            return newNode;
        }

        private ItemsCollection m_Items;

        private class ItemsCollection : IList<ComparisonItem>
        {
            public ItemsCollection(ComparisonItem parent)
            {
                m_Parent = parent;
            }

            private ComparisonItem m_Parent;
            private List<ComparisonItem> m_Items = new List<ComparisonItem>();

            #region IList<ComparisonItem> Members

            public int IndexOf(ComparisonItem item)
            {
                return m_Items.IndexOf(item);
            }

            public void Insert(int index, ComparisonItem item)
            {
                item.Parent = m_Parent;
                m_Items.Insert(index, item);
            }

            public void RemoveAt(int index)
            {
                m_Items.RemoveAt(index);
            }

            public ComparisonItem this[int index]
            {
                get
                {
                    return m_Items[index];
                }
                set
                {
                    value.Parent = m_Parent;
                    m_Items[index] = value;
                }
            }

            #endregion

            #region ICollection<ComparisonItem> Members

            public void Add(ComparisonItem item)
            {
                item.Parent = m_Parent;
                m_Items.Add(item);
            }

            public void Clear()
            {
                m_Items.Clear();
            }

            public bool Contains(ComparisonItem item)
            {
                return m_Items.Contains(item);
            }

            public void CopyTo(ComparisonItem[] array, int arrayIndex)
            {
                m_Items.CopyTo(array, arrayIndex);
            }

            public int Count
            {
                get { return m_Items.Count; }
            }

            public bool IsReadOnly
            {
                get { return false; }
            }

            public bool Remove(ComparisonItem item)
            {
                return m_Items.Remove(item);
            }

            #endregion

            #region IEnumerable<ComparisonItem> Members

            public IEnumerator<ComparisonItem> GetEnumerator()
            {
                return m_Items.GetEnumerator();
            }

            #endregion

            #region IEnumerable Members

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return m_Items.GetEnumerator();
            }

            #endregion
        }

    }

    enum ResultNodeType
    {
        Object,
        ObjectsCollection,
        PropertyDef,
        FakeNode        
    }

    enum ComparisonStatus
    {
        Match,
        Modified,
        Added,
        Deleted
    }

    class ComparisonSide
    {
        private object m_Obj;

        public object Object
        {
            get { return m_Obj; }
            set { m_Obj = value; }
        }

        public string Presentation { get; set; }
        public override string ToString()
        {
            if (Presentation == String.Empty)
                return "< текст не задан >";

            return Presentation;
        }
    }

    class ComparisonResult : ComparisonItem
    {
        public ComparisonResult() : base()
        {
        }
    }

    class FileComparisonPerformer : IComparisonPerformer, IDisposable
    {

        public FileComparisonPerformer(string LeftFile, string RightFile)
        {
            _LeftFile = new V8MetadataContainer(LeftFile);
            _RightFile = new V8MetadataContainer(RightFile);
        }

        public ComparisonResult Perform()
        {
            return Perform(ComparisonPerformer.MatchingMode.ByID);
        }

        public ComparisonResult Perform(ComparisonPerformer.MatchingMode Mode)
        {
            InstantiateObject(_LeftFile, ref _LeftObject);
            InstantiateObject(_RightFile, ref _RightObject);

            m_Performer = new ComparisonPerformer(_LeftObject, _RightObject);
            
            return m_Performer.Perform(Mode);
        }

        private void InstantiateObject(V8MetadataContainer cnt, ref IMDTreeItem obj)
        {
            if (obj == null)
            {
                var tmpObj = cnt.RaiseObject();

                if (tmpObj is IMDTreeItem)
                {
                    obj = (IMDTreeItem)tmpObj;
                }
                else
                {
                    throw new InvalidOperationException(String.Format("Comparison of '{0}' isn't supported", obj.GetType().ToString()));
                }
            }
        }

        private IMDTreeItem _LeftObject;
        private IMDTreeItem _RightObject;

        private ComparisonPerformer m_Performer;

        private V8MetadataContainer _LeftFile;
        private V8MetadataContainer _RightFile;


        #region IDisposable Members

        public void Dispose()
        {
            LocalDispose(_LeftFile);
            LocalDispose(_RightFile);

            _LeftObject  = null;
            _RightObject = null;
        }

        private void LocalDispose(IDisposable Obj)
        {
            if (Obj != null) Obj.Dispose();
        }

        #endregion
    }

    interface IComparisonPerformer
	{
        ComparisonResult Perform();

        ComparisonResult Perform(ComparisonPerformer.MatchingMode Mode);
	}
    
    

}
