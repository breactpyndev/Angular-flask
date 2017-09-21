﻿using CacheManager.Core;
using PagedList.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using VirtoCommerce.Storefront.AutoRestClients.CustomerModuleApi;
using VirtoCommerce.Storefront.AutoRestClients.StoreModuleApi;
using VirtoCommerce.Storefront.Common;
using VirtoCommerce.Storefront.Converters;
using VirtoCommerce.Storefront.Model;
using VirtoCommerce.Storefront.Model.Common;
using VirtoCommerce.Storefront.Model.Common.Events;
using VirtoCommerce.Storefront.Model.Customer;
using VirtoCommerce.Storefront.Model.Customer.Services;
using VirtoCommerce.Storefront.Model.Order;
using VirtoCommerce.Storefront.Model.Order.Events;
using VirtoCommerce.Storefront.Model.Order.Services;
using VirtoCommerce.Storefront.Model.Quote;
using VirtoCommerce.Storefront.Model.Quote.Events;
using VirtoCommerce.Storefront.Model.Quote.Services;
using VirtoCommerce.Storefront.Model.Stores;
using VirtoCommerce.Storefront.Model.Subscriptions;
using VirtoCommerce.Storefront.Model.Subscriptions.Services;
using customerDto = VirtoCommerce.Storefront.AutoRestClients.CustomerModuleApi.Models;

namespace VirtoCommerce.Storefront.Services
{
    public class CustomerService : ICustomerService, IEventHandler<OrderPlacedEvent>, IEventHandler<QuoteRequestUpdatedEvent>
    {
        private readonly ICustomerModule _customerApi;
        private readonly ICustomerOrderService _orderService;
        private readonly IQuoteService _quoteService;
        private readonly IStoreModule _storeApi;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ICacheManager<object> _cacheManager;
        private const string _customerOrdersCacheRegionFormat = "customer/{0}/orders/region";
        private const string _customerQuotesCacheRegionFormat = "customer/{0}/quotes/region";
        private const string _customerSubscriptionCacheRegionFormat = "customer/{0}/subscriptions/region";
        private const string _customerCacheKeyFormat = "customer/{0}";
        private const string _customerCacheRegionFormat = "customer/{0}/region";

        public CustomerService(ICustomerModule customerApi, ICustomerOrderService orderService,
            IQuoteService quoteService, IStoreModule storeApi, ISubscriptionService subscriptionService, ICacheManager<object> cacheManager)
        {
            _customerApi = customerApi;
            _orderService = orderService;
            _quoteService = quoteService;
            _storeApi = storeApi;
            _cacheManager = cacheManager;
            _subscriptionService = subscriptionService;
        }

        #region ICustomerService Members

        public virtual async Task<CustomerInfo> GetCustomerByIdAsync(string customerId)
        {
            var retVal = await _cacheManager.GetAsync(string.Format(_customerCacheKeyFormat, customerId), string.Format(_customerCacheRegionFormat, customerId), async () =>
            {
                //TODO: Make parallels call
                var contact = await _customerApi.GetMemberByIdAsync(customerId);
                CustomerInfo result = null;
                if (contact != null)
                {
                    result = contact.ToCustomerInfo();
                }
                return result;
            });

            if (retVal != null)
            {
                retVal = retVal.JsonClone();
                retVal.Orders = GetCustomerOrders(retVal);
                retVal.QuoteRequests = GetCustomerQuotes(retVal);
                retVal.Subscriptions = GetCustomerSubscriptions(retVal);
            }

            return retVal;
        }
        
        public virtual async Task CreateCustomerAsync(CustomerInfo customer)
        {
            var contact = customer.ToCustomerContactDto();
            await _customerApi.CreateContactAsync(contact);
        }
        
        public virtual async Task UpdateCustomerAsync(CustomerInfo customer)
        {
            if (customer.MemberType != typeof(customerDto.Contact).Name)
                throw new HttpRequestException("Can't update customer which member type is not " + typeof(customerDto.Contact).Name);

            var contact = customer.ToCustomerContactDto();
            await _customerApi.UpdateContactAsync(contact);
            //Invalidate cache
            _cacheManager.ClearRegion(string.Format(_customerCacheRegionFormat, customer.Id));
        }

        public async Task UpdateAddressesAsync(CustomerInfo customer)
        {
            await _customerApi.UpdateAddessesAsync(customer.Id, customer.Addresses.Select(x => x.ToCustomerAddressDto()).ToList());
            //Invalidate cache
            _cacheManager.ClearRegion(string.Format(_customerCacheRegionFormat, customer.Id));
        }

        public virtual async Task<bool> CanLoginOnBehalfAsync(string storeId, string customerId)
        {
            var info = await _storeApi.GetLoginOnBehalfInfoAsync(storeId, customerId);
            return info.CanLoginOnBehalf == true;
        }

        public virtual async Task<Vendor[]> GetVendorsByIdsAsync(Store store, Language language, params string[] vendorIds)
        {
            return (await _customerApi.GetVendorsByIdsAsync(vendorIds)).Select(x => x.ToVendor(language, store)).ToArray();
        }

        public virtual Vendor[] GetVendorsByIds(Store store, Language language, params string[] vendorIds)
        {
            var retVal = _customerApi.GetVendorsByIds(vendorIds).Select(x => x.ToVendor(language, store)).ToArray();
            return retVal;
        }

        public virtual IPagedList<Vendor> SearchVendors(Store store, Language language, string keyword, int pageNumber, int pageSize, IEnumerable<SortInfo> sortInfos)
        {
            // TODO: implement indexed search for vendors
            //TODO: Add caching for vendors
            var criteria = new customerDto.MembersSearchCriteria
            {
                SearchPhrase = keyword,
                DeepSearch = true,
                Skip = (pageNumber - 1) * pageSize,
                Take = pageSize
            };

            if (!sortInfos.IsNullOrEmpty())
            {
                criteria.Sort = SortInfo.ToString(sortInfos);
            }
            var vendorSearchResult = _customerApi.SearchVendors(criteria);
            var vendors = vendorSearchResult.Vendors.Select(x => x.ToVendor(language, store));       
            return new StaticPagedList<Vendor>(vendors, pageNumber, pageSize, vendorSearchResult.TotalCount.Value);
        }
        #endregion

        #region IEventHandler<OrderPlacedEvent> 
        public virtual async Task Handle(OrderPlacedEvent @event)
        {
            if (@event.Order != null)
            {
                //Invalidate cache
                _cacheManager.ClearRegion(string.Format(_customerOrdersCacheRegionFormat, @event.Order.CustomerId));
                _cacheManager.ClearRegion(string.Format(_customerSubscriptionCacheRegionFormat, @event.Order.CustomerId));

                //Add addresses to contact profile
                if (@event.WorkContext.CurrentCustomer.IsRegisteredUser)
                {
                    @event.WorkContext.CurrentCustomer.Addresses.AddRange(@event.Order.Addresses);
                    @event.WorkContext.CurrentCustomer.Addresses.AddRange(@event.Order.Shipments.Select(x => x.DeliveryAddress));

                    foreach (var address in @event.WorkContext.CurrentCustomer.Addresses)
                    {
                        address.Name = address.ToString();
                    }

                    await UpdateAddressesAsync(@event.WorkContext.CurrentCustomer);
                }
            }
        }
        #endregion

        #region IEventHandler<QuoteRequestUpdatedEvent>

        public virtual Task Handle(QuoteRequestUpdatedEvent @event)
        {
            if (@event.QuoteRequest != null)
            {
                //Invalidate cache
                _cacheManager.ClearRegion(string.Format(_customerQuotesCacheRegionFormat, @event.QuoteRequest.CustomerId));
            }
            return Task.CompletedTask;
        }

        #endregion

        protected virtual IMutablePagedList<QuoteRequest> GetCustomerQuotes(CustomerInfo customer)
        {
            Func<int, int, IEnumerable<SortInfo>, IPagedList<QuoteRequest>> quotesGetter = (pageNumber, pageSize, sortInfos) =>
            {
                var quoteSearchCriteria = new QuoteSearchCriteria
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    Sort = sortInfos.ToString(),
                    CustomerId = customer.Id                   
                };
                var cacheKey = "GetCustomerQuotes-" + quoteSearchCriteria.GetHashCode();
                return _cacheManager.Get(cacheKey, string.Format(_customerQuotesCacheRegionFormat, customer.Id), () =>  _quoteService.SearchQuotes(quoteSearchCriteria), cacheNullValue: false);
            };

            return new MutablePagedList<QuoteRequest>(quotesGetter, 1, QuoteSearchCriteria.DefaultPageSize);
        }

        protected virtual IMutablePagedList<CustomerOrder> GetCustomerOrders(CustomerInfo customer)
        {
            var orderSearchcriteria = new OrderSearchCriteria
            {
                CustomerId = customer.Id             
            };

            Func<int, int, IEnumerable<SortInfo>, IPagedList<CustomerOrder>> ordersGetter = (pageNumber, pageSize, sortInfos) =>
            {
                orderSearchcriteria.PageNumber = pageNumber;
                orderSearchcriteria.PageSize = pageSize;
                var cacheKey = "GetCustomerOrders-" + orderSearchcriteria.GetHashCode();
                return _cacheManager.Get(cacheKey, string.Format(_customerOrdersCacheRegionFormat, customer.Id), () => _orderService.SearchOrders(orderSearchcriteria));
            };
            return new MutablePagedList<CustomerOrder>(ordersGetter, 1, OrderSearchCriteria.DefaultPageSize);
        }

        protected virtual IMutablePagedList<Subscription> GetCustomerSubscriptions(CustomerInfo customer)
        {
            var subscriptionSearchcriteria = new SubscriptionSearchCriteria
            {
                CustomerId = customer.Id             
            };

            Func<int, int, IEnumerable<SortInfo>, IPagedList<Subscription>> subscriptionGetter = (pageNumber, pageSize, sortInfos) =>
            {
                subscriptionSearchcriteria.PageNumber = pageNumber;
                subscriptionSearchcriteria.PageSize = pageSize;
                var cacheKey = "GetSubscriptions-" + subscriptionSearchcriteria.GetHashCode();
                return _cacheManager.Get(cacheKey, string.Format(_customerSubscriptionCacheRegionFormat, customer.Id), () => _subscriptionService.SearchSubscription(subscriptionSearchcriteria));
            };
            return new MutablePagedList<Subscription>(subscriptionGetter, 1, SubscriptionSearchCriteria.DefaultPageSize);
        }
    }
}

