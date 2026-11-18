Option Strict On
Option Explicit On

Imports System
Imports System.Windows.Forms
Imports Velopack

''' <summary>
''' Einstiegspunkt der Anwendung AutoBOX.exe.
''' STAThread ist fuer Windows Forms und die COM-Kommunikation mit Solid Edge erforderlich.
''' </summary>
Module Program

    <STAThread>
    Public Sub Main()
        ' Velopack: MUSS die erste Anweisung sein (Install-/Update-Hooks).
        VelopackApp.Build().Run()

        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New MainForm())
    End Sub

End Module
