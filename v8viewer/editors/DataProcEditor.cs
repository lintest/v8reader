﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using V8Reader.Core;

namespace V8Reader.Editors
{
    sealed class DataProcEditor : CustomEditor, ICustomEditor
    {
        public DataProcEditor(MDDataProcessor DataProc)
            : base()
        {
            m_Object = DataProc;
        }

        private DataProcEditorWnd m_Window;
        private MDDataProcessor m_Object;

        public void Edit()
        {
            Edit(null);
        }

        public void Edit(Window Owner)
        {
            if (m_Window == null)
            {
                InitWindow();
            }

            m_Window.Owner = Owner;
            m_Window.Show();

        }

        private void InitWindow()
        {
            m_Window = new DataProcEditorWnd(m_Object);
            m_Window.Closing += m_Window_Closing;
        }

        private void  m_Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            OnEditComplete(true, m_Object);
        }

    }
}
