Option Strict On
Option Explicit On

Imports System
Imports System.Windows.Forms

''' <summary>
''' Einstiegspunkt der Anwendung AutoBOX.exe.
''' STAThread ist fuer Windows Forms und die COM-Kommunikation mit Solid Edge erforderlich.
''' </summary>
Module Program

    <STAThread>
    Public Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New MainForm())
    End Sub

End Module
