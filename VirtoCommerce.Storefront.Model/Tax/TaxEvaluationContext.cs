﻿using System.Collections.Generic;
using VirtoCommerce.Storefront.Model.Common;
using VirtoCommerce.Storefront.Model.Security;

namespace VirtoCommerce.Storefront.Model.Tax
{
    public partial class TaxEvaluationContext : ValueObject
    {
        public TaxEvaluationContext(string storeId)
        {
            StoreId = storeId;
            Lines = new List<TaxLine>();
            StoreTaxCalculationEnabled = true;
        }
        //It not a context identifier
        public string Id { get; set; }
        public string StoreId { get; set; }

        public string Code { get; set; }

         public string Type { get; set; }

        public User Customer { get; set; }

        public Address Address { get; set; }

        public Currency Currency { get; set; }

        public IList<TaxLine> Lines { get; set; }

        public bool StoreTaxCalculationEnabled { get; set; }
       
    }
}
