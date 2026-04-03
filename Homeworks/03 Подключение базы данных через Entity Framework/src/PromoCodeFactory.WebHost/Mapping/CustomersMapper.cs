using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.WebHost.Models.Customers;
using PromoCodeFactory.WebHost.Models.Preferences;
using PromoCodeFactory.WebHost.Models.PromoCodes;

namespace PromoCodeFactory.WebHost.Mapping;

public static class CustomersMapper
{
    public static CustomerShortResponse ToCustomerShortResponse(Customer customer)
    {
        return new CustomerShortResponse(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.Preferences.Select(PreferencesMapper.ToPreferenceShortResponse).ToList()
        );
    }

    public static CustomerResponse ToCustomerResponse(Customer customer, IEnumerable<PromoCode> promoCodes)
    {
        var promoCodeDict = promoCodes.ToDictionary(p => p.Id);
        return new CustomerResponse(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.Preferences.Select(PreferencesMapper.ToPreferenceShortResponse).ToList(),
            customer.CustomerPromoCodes
                .Select(cpc => ToCustomerPromoCodeResponse(cpc, promoCodeDict.GetValueOrDefault(cpc.PromoCodeId)))
                .Where(r => r != null)
                .ToList()!
        );
    }

    private static CustomerPromoCodeResponse ToCustomerPromoCodeResponse(CustomerPromoCode cpc, PromoCode? promoCode)
    {
        if (promoCode == null)
            throw new InvalidOperationException($"PromoCode with Id {cpc.PromoCodeId} not found");
        return new CustomerPromoCodeResponse(
            cpc.Id,
            promoCode.Code,
            promoCode.ServiceInfo,
            promoCode.PartnerName,
            promoCode.BeginDate,
            promoCode.EndDate,
            promoCode.PartnerManager.Id,
            promoCode.Preference.Id,
            cpc.CreatedAt,
            cpc.AppliedAt
        );
    }
}
