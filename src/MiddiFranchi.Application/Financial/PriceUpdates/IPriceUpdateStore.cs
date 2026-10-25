namespace MiddiFranchi.Application.Financial;

public interface IPriceUpdateStore
{
    PriceUpdateSettings GetSettings();

    void SaveSettings(PriceUpdateSettings settings);

    PriceUpdateStatus GetStatus();

    void SaveStatus(PriceUpdateStatus status);
}
