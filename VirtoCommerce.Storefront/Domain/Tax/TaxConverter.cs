﻿using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Storefront.Model;
using VirtoCommerce.Storefront.Model.Catalog;
using VirtoCommerce.Storefront.Model.Common;
using VirtoCommerce.Storefront.Model.Tax;
using coreDto = VirtoCommerce.Storefront.AutoRestClients.CoreModuleApi.Models;

namespace VirtoCommerce.Storefront.Domain
{
    public static class TaxConverterExtension
    {
        public static TaxConverter TaxConverterInstance
        {
            get
            {
                return new TaxConverter();
            }
        }

        public static coreDto.TaxEvaluationContext ToTaxEvaluationContextDto(this TaxEvaluationContext taxContext)
        {
            return TaxConverterInstance.ToTaxEvaluationContextDto(taxContext);
        }

        public static TaxEvaluationContext ToTaxEvaluationContext(this WorkContext workContext, IEnumerable<Product> products = null)
        {
            return TaxConverterInstance.ToTaxEvaluationContext(workContext, products);
        }

        public static TaxRate ToTaxRate(this coreDto.TaxRate taxRateDto, Currency currency)
        {
            return TaxConverterInstance.ToTaxRate(taxRateDto, currency);
        }
    }

    public partial class TaxConverter
    {
        public virtual TaxRate ToTaxRate(coreDto.TaxRate taxRateDto, Currency currency)
        {
            var result = new TaxRate(currency)
            {
                Rate = new Money(taxRateDto.Rate.Value, currency)
            };

            if (taxRateDto.Line != null)
            {
                result.Line = new TaxLine(currency);
                result.Line.Code = taxRateDto.Line.Code;
                result.Line.Id = taxRateDto.Line.Id;
                result.Line.Name = taxRateDto.Line.Name;
                result.Line.Quantity = taxRateDto.Line.Quantity ?? 1;
                result.Line.TaxType = taxRateDto.Line.TaxType;

                result.Line.Amount = new Money(taxRateDto.Line.Amount.Value, currency);
                result.Line.Price = new Money(taxRateDto.Line.Price.Value, currency);
            }

            return result;
        }

        public virtual coreDto.TaxEvaluationContext ToTaxEvaluationContextDto(TaxEvaluationContext taxContext)
        {
            var retVal = new coreDto.TaxEvaluationContext();
            retVal.Code = taxContext.Code;
            retVal.Id = taxContext.Id;
            retVal.Type = taxContext.Type;
       
            if (taxContext.Address != null)
            {
                retVal.Address = taxContext.Address.ToCoreAddressDto();
            }
            if (taxContext.Customer != null && taxContext.Customer.Contact != null)
            {
                var contact = taxContext.Customer.Contact.Value;
                if (contact != null)
                {
                    retVal.Customer = contact.ToCoreContactDto();
                    retVal.Customer.MemberType = "Contact";
                }          
            }
            if (taxContext.Currency != null)
            {
                retVal.Currency = taxContext.Currency.Code;
            }

            retVal.Lines = new List<coreDto.TaxLine>();
            if (!taxContext.Lines.IsNullOrEmpty())
            {
                foreach (var line in taxContext.Lines)
                {
                    var serviceModelLine = new coreDto.TaxLine();
                    serviceModelLine.Code = line.Code;
                    serviceModelLine.Name = line.Name;
                    serviceModelLine.Quantity = line.Quantity;
                    serviceModelLine.TaxType = line.TaxType;
                    serviceModelLine.Amount = (double)line.Amount.Amount;
                    serviceModelLine.Price = (double)line.Price.Amount;

                    retVal.Lines.Add(serviceModelLine);
                }
            }
            return retVal;
        }


        public virtual TaxEvaluationContext ToTaxEvaluationContext(WorkContext workContext, IEnumerable<Product> products = null)
        {
            var result = new TaxEvaluationContext(workContext.CurrentStore.Id);
            result.Id = workContext.CurrentStore.Id;
            result.Currency = workContext.CurrentCurrency;
            result.Type = "";
       
            result.Customer = workContext.CurrentUser;
            result.StoreTaxCalculationEnabled = workContext.CurrentStore.TaxCalculationEnabled;
            if(workContext.CurrentUser.Contact != null)
            {
                var contact = workContext.CurrentUser.Contact.Value;
                if(contact != null)
                {
                    result.Address = contact.DefaultBillingAddress;
                }
            }
            if (products != null)
            {
                result.Lines = products.SelectMany(x => x.ToTaxLines()).ToList();
            }
            return result;
        }


    }
}
