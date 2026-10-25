namespace MiddiFranchi.Application.DataManagement;

public interface IPriceUpdateBackupParticipant
{
    string ExportDocument();

    void ImportDocument(string json);
}
