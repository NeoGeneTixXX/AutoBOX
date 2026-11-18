Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports System.Text

''' <summary>
''' Einfaches Datei-Log unter %LOCALAPPDATA%\AutoBOX\logs\ (eine Datei pro Tag).
''' Schreibfehler werden verschluckt – das Log darf das Programm nie stoeren.
''' </summary>
Public NotInheritable Class Log

    Private Shared ReadOnly _sperre As New Object()

    Private Sub New()
    End Sub

    ''' <summary>Ordner der Log-Dateien.</summary>
    Public Shared ReadOnly Property Ordner As String
        Get
            Return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoBOX", "logs")
        End Get
    End Property

    Public Shared Sub Schreibe(text As String)
        Try
            SyncLock _sperre
                Directory.CreateDirectory(Ordner)
                Dim datei As String = Path.Combine(Ordner, "AutoBOX_" & DateTime.Now.ToString("yyyy-MM-dd") & ".log")
                File.AppendAllText(datei,
                                   DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & "  " & text & Environment.NewLine,
                                   Encoding.UTF8)
            End SyncLock
        Catch
            ' Log-Fehler ignorieren
        End Try
    End Sub

    Public Shared Sub Schreibe(text As String, ex As Exception)
        Schreibe(text & ": " & If(ex IsNot Nothing, ex.ToString(), "(keine Details)"))
    End Sub

End Class
