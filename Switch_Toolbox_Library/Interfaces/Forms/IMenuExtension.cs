﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Library.Forms;
using System.Windows.Forms;

namespace Toolbox.Library
{
    //Based on Exelix's menu ext
    public interface IMenuExtension
    {
        STToolStripItem[] FileMenuExtensions { get; }
        STToolStripItem[] ToolsMenuExtensions { get; }
        STToolStripItem[] TitleBarExtensions { get; }
    }
    public interface IFileMenuExtension
    {
        STToolStripItem[] NewFileMenuExtensions { get; }
        STToolStripItem[] NewFromFileMenuExtensions { get; }
        STToolStripItem[] CompressionMenuExtensions { get; }
        STToolStripItem[] ToolsMenuExtensions { get; }
        STToolStripItem[] TitleBarExtensions { get; }
        STToolStripItem[] ExperimentalMenuExtensions { get; }
        STToolStripItem[] EditMenuExtensions { get; }
        ToolStripButton[] IconButtonMenuExtensions { get; }
    }
}