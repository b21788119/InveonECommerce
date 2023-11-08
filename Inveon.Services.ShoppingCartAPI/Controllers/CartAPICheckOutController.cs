using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Iyzipay.Request;
using Iyzipay.Model;
using Iyzipay;
using Inveon.Services.ShoppingCartAPI.Messages;
using Inveon.Services.ShoppingCartAPI.RabbitMQSender;
using Inveon.Services.ShoppingCartAPI.Repository;
using Inveon.Services.ShoppingCartAPI.Models.Dto;
using System.Diagnostics.Metrics;

namespace Inveon.Service.ShoppingCartAPI.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/cartc")]
    public class CartAPICheckOutController : ControllerBase
    {

        private readonly ICartRepository _cartRepository;
        private readonly ICouponRepository _couponRepository;
        // private readonly IMessageBus _messageBus;
        protected ResponseDto _response;
        private readonly IRabbitMQCartMessageSender _rabbitMQCartMessageSender;
        // IMessageBus messageBus,
        public CartAPICheckOutController(ICartRepository cartRepository,
            ICouponRepository couponRepository, IRabbitMQCartMessageSender rabbitMQCartMessageSender)
        {
            _cartRepository = cartRepository;
            _couponRepository = couponRepository;
            _rabbitMQCartMessageSender = rabbitMQCartMessageSender;
            //_messageBus = messageBus;
            this._response = new ResponseDto();
        }

        [HttpPost]
        [Authorize]
        public async Task<object> Checkout([FromBody] CheckoutHeaderDto checkoutHeader)




        {
            try
            {
                CartDto cartDto = await _cartRepository.GetCartByUserId(checkoutHeader.UserId);
                if (cartDto == null)
                {
                    return BadRequest();
                }

                if (!string.IsNullOrEmpty(checkoutHeader.CouponCode))
                {
                    CouponDto coupon = await _couponRepository.GetCoupon(checkoutHeader.CouponCode);
                    if (checkoutHeader.DiscountTotal != coupon.DiscountAmount)
                    {
                        _response.IsSuccess = false;
                        _response.ErrorMessages = new List<string>() { "Coupon Price has changed, please confirm" };
                        _response.DisplayMessage = "Coupon Price has changed, please confirm";
                        return _response;
                    }
                }

                checkoutHeader.CartDetails = cartDto.CartDetails;
                //logic to add message to process order.
                // await _messageBus.PublishMessage(checkoutHeader, "checkoutqueue");

                ////rabbitMQ

                Payment payment = OdemeIslemi(checkoutHeader);
                _rabbitMQCartMessageSender.SendMessage(checkoutHeader, "checkoutqueue");
                await _cartRepository.ClearCart(checkoutHeader.UserId);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        public Payment OdemeIslemi(CheckoutHeaderDto checkoutHeaderDto)
        {

            CartDto cartDto = _cartRepository.GetCartByUserIdNonAsync(checkoutHeaderDto.UserId);

            Options options = new Options();

            // Updating the API options here according to my test account.
            options.ApiKey = "sandbox-vL543CPlFNSa6BWqBGXDF4bvV1Ky4Nr6";
            options.SecretKey = "A1dyQcCo6dKCdeLOuyTjTRB7zjcuqBAj";
            options.BaseUrl = "https://sandbox-api.iyzipay.com";

            CreatePaymentRequest request = new CreatePaymentRequest();
            request.ConversationId = new Random().Next(1111, 9999).ToString();
            request.Locale = Locale.TR.ToString();
            request.Price = checkoutHeaderDto.OrderTotal.ToString();
            request.PaidPrice = Math.Ceiling(checkoutHeaderDto.OrderTotal).ToString();

            request.Currency = Currency.TRY.ToString();
            request.Installment = 1;
            request.PaymentChannel = PaymentChannel.WEB.ToString();
            request.PaymentGroup = PaymentGroup.PRODUCT.ToString();

            PaymentCard paymentCard = new PaymentCard();
            paymentCard.CardHolderName = checkoutHeaderDto.FirstName + " " + checkoutHeaderDto.LastName;
            paymentCard.CardNumber = checkoutHeaderDto.CardNumber;
            paymentCard.ExpireMonth = checkoutHeaderDto.ExpiryMonth;
            paymentCard.ExpireYear = checkoutHeaderDto.ExpiryYear;
            paymentCard.Cvc = checkoutHeaderDto.CVV;
            paymentCard.RegisterCard = 0;
            request.PaymentCard = paymentCard;

            Buyer buyer = new Buyer();
            buyer.Id = checkoutHeaderDto.UserId;
            buyer.Name = checkoutHeaderDto.FirstName;
            buyer.Surname = checkoutHeaderDto.LastName;
            buyer.GsmNumber = checkoutHeaderDto.Phone;
            buyer.Email = checkoutHeaderDto.Email;
            buyer.IdentityNumber = "74300864791";
            buyer.LastLoginDate = "2015-10-05 12:43:35";
            buyer.RegistrationDate = "2013-04-21 15:12:09";
            buyer.RegistrationAddress = "Nidakule Göztepe, Merdivenköy Mah. Bora Sok. No:1";
            buyer.Ip = "85.34.78.112";
            buyer.City = "Istanbul";
            buyer.Country = "Turkey";
            buyer.ZipCode = "34732";
            request.Buyer = buyer;

            Address shippingAddress = new Address();
            shippingAddress.ContactName = checkoutHeaderDto.FirstName + " " + checkoutHeaderDto.LastName;
            shippingAddress.City = "Istanbul";
            shippingAddress.Country = "Turkey";
            shippingAddress.Description = "Nidakule Göztepe, Merdivenköy Mah. Bora Sok. No:1";
            shippingAddress.ZipCode = "34742";
            request.ShippingAddress = shippingAddress;

            Address billingAddress = new Address();
            billingAddress.ContactName = checkoutHeaderDto.FirstName + " " + checkoutHeaderDto.LastName;
            billingAddress.City = "Istanbul";
            billingAddress.Country = "Turkey";
            billingAddress.Description = "Nidakule Göztepe, Merdivenköy Mah. Bora Sok. No:1";
            billingAddress.ZipCode = "34742";
            request.BillingAddress = billingAddress;

            List<BasketItem> basketItems = new List<BasketItem>();
            int helper = 101;
            foreach (CartDetailsDto detail in checkoutHeaderDto.CartDetails)
            {
                BasketItem basketItem = new BasketItem();
                basketItem.Id = "BI"+helper.ToString();
                basketItem.Name = detail.Product.Name.ToString();
                basketItem.Category1 = detail.Product.CategoryName;
                basketItem.ItemType = BasketItemType.PHYSICAL.ToString();
                basketItem.Price = (detail.Product.Price * detail.Count).ToString();
                basketItems.Add(basketItem);
                helper++;

            }
            request.BasketItems = basketItems;
            Payment payment = Payment.Create(request, options);
            // Binding basket items to our request.
            return payment;
        }
    }
}
