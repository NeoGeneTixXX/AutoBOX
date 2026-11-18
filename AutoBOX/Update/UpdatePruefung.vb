Option Strict On
Option Explicit On

Imports System
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Velopack
Imports Velopack.Sources

''' <summary>
''' Auto-Update ueber Velopack mit GitHub Releases als Quelle.
'''
''' Ablauf: Pruefung im Hintergrund beim Programmstart -> bei neuer Version
''' Rueckfrage mit aktueller und neuer Versionsnummer -> Download, Installation,
''' Neustart. Kein Internet, Fehler oder ein Start ausserhalb einer
''' Velopack-Installation (z. B. Debug) werden nur ins Log geschrieben.
''' </summary>
Public NotInheritable Class UpdatePruefung

    Private Const REPO_URL As String = "https://github.com/NeoGeneTixXX/AutoBOX"

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Startet die Update-Pruefung. Muss im UI-Thread aufgerufen werden
    ''' (der Dialog wird dort angezeigt); die Netzwerkzugriffe laufen im Hintergrund.
    ''' </summary>
    Public Shared Async Function PruefeAsync(besitzer As IWin32Window) As Task
        Try
            Dim mgr As New UpdateManager(New GithubSource(REPO_URL, Nothing, False))

            If Not mgr.IsInstalled Then
                Log.Schreibe("Update-Pruefung uebersprungen: Programm ist nicht ueber Velopack installiert.")
                Return
            End If

            Dim info As UpdateInfo = Await Task.Run(Function() mgr.CheckForUpdatesAsync())
            If info Is Nothing Then
                Log.Schreibe("Update-Pruefung: Version " & mgr.CurrentVersion.ToString() & " ist aktuell.")
                Return
            End If

            Dim aktuell As String = mgr.CurrentVersion.ToString()
            Dim neu As String = info.TargetFullRelease.Version.ToString()
            Log.Schreibe("Update verfuegbar: " & aktuell & " -> " & neu)

            Dim antwort As DialogResult = MessageBox.Show(
                besitzer,
                "Eine neue Version von AutoBOX ist verfügbar." & Environment.NewLine & Environment.NewLine &
                "Installierte Version:  " & aktuell & Environment.NewLine &
                "Neue Version:          " & neu & Environment.NewLine & Environment.NewLine &
                "Jetzt herunterladen, installieren und AutoBOX neu starten?",
                "AutoBOX – Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information)

            If antwort <> DialogResult.Yes Then
                Log.Schreibe("Update vom Benutzer abgelehnt.")
                Return
            End If

            Await Task.Run(Function() mgr.DownloadUpdatesAsync(info))
            Log.Schreibe("Update " & neu & " heruntergeladen – wird installiert, Neustart folgt.")
            mgr.ApplyUpdatesAndRestart(info.TargetFullRelease)
        Catch ex As Exception
            ' Kein Internet, GitHub nicht erreichbar o. Ae.: Programm laeuft normal weiter.
            Log.Schreibe("Update-Pruefung fehlgeschlagen", ex)
        End Try
    End Function

End Class
