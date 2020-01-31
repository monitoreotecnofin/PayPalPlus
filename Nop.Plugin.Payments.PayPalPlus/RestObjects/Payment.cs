using System;
using System.Collections.Generic;
using System.Text;

namespace Nop.Plugin.Payments.PayPalPlus.RestObjects
{

    public class Payment
    {

        //public string id { get; set; }
        public string intent { get; set; }

        public AplicationContext application_context;
        //public string state { get; set; }
        //public string Experience_profile_id { get; set; }

        public Payer payer;

        public List<Transactions> transactions { get; set; }

        //public DateTime create_time { get; set; }

        public RedirectUrlsInfo redirect_urls { get; set; }
    }

    public class AplicationContext
    {
        public string shipping_preference { get; set; }

    }

    public class Payer
    {
        private string name = "paypal";
        public string payment_method
        {
            get { return name; }
            set { name = value; }
        }
    }

    public class Transactions
    {
        public AmountInfo amount { get; set; }
        // public PayeeInfo Payee { get; set; }
        public string description { get; set; }
        public string custom { get; set; }
        public PaymentOptions payment_options;
        public Items item_list { get; set; }
        public string notify_url { get; set; }
    }

    public class Sale
    {
        public string Id { get; set; }
        public string State { get; set; }
        public AmountInfo Amount { get; set; }
        public string Payment_mode { get; set; }
        public string Protection_eligibility { get; set; }
        public string Protection_eligibility_type { get; set; }

        public TransactionFee Transaction_fee { get; set; }

        public string Receipt_id { get; set; }
        public string Parent_payment { get; set; }

        public DateTime Create_time { get; set; }
        public DateTime Update_time { get; set; }

        public List<LinksInfo> Links { get; set; }

        public string Soft_descriptor { get; set; }
    }

    public class RelatedResource
    {
        public Sale Sale { get; set; }
    }

    public class TransactionFee
    {
        public string Value { get; set; }
        public string Currency { get; set; }
    }

    public class PayeeInfo
    {
        public string Merchant_id;
    }

    public class AmountInfo
    {
        public string currency { get; set; }
        public DetailsAmountInfo details;
        public string total { get; set; }
    }
    public class DetailsAmountInfo
    {
        public string shipping { get; set; }
        public string subtotal { get; set; }

        public string tax { get; set; }
        public string shipping_discount { get; set; }

    }
    public class PaymentOptions
    {
        public string allowed_payment_method = "IMMEDIATE_PAY";

    }
    public class Items
    {
        public List<Item> items;
        public ShippingAddressInfo shipping_address;
    }
    public class Item
    {
        public string name { get; set; }
        public string description { get; set; }
        public string quantity { get; set; }
        public string price { get; set; }
        public string sku { get; set; }
        public string currency { get; set; }
    }

    public class ShippingAddressInfo
    {
        public string recipient_name { get; set; }
        public string line1 { get; set; }
        public string line2 { get; set; }
        public string city { get; set; }
        public string country_code { get; set; }
        public string postal_code { get; set; }
        public string state { get; set; }
        public string phone { get; set; }
    }
    public class RedirectUrlsInfo
    {
        public string return_url { get; set; }
        public string cancel_url { get; set; }
    }
    public class LinksInfo
    {
        public string Href { get; set; }
        public string Rel { get; set; }
        public string Method { get; set; }
    }

    public class PayerIDInfo
    {
        public string Payer_id { get; set; }
    }


}
