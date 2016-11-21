using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DoanMVC.Models;
using Model.DAO;
using System.Web.Script.Serialization;
using Model.EF;
using System.Configuration;
using Common;
using DoanMVC.Common;

namespace DoanMVC.Controllers
{
    public class CartsController : Controller
    {
        private const string CartSession = "CartSession";
        // GET: Carts
        public ActionResult Index()
        {
            var cart = Session[CartSession];
            var list = new List<CartItem>();
            if (cart != null)
            {
                list = (List<CartItem>)cart;
            }
            return View(list);
        }
        [HttpPost]
        public JsonResult Delete(long id)
        {
            var sessionCart = (List<CartItem>)Session[CartSession];
            sessionCart.RemoveAll(x => x.Product.ID == id);
            Session[CartSession] = sessionCart;
            return Json(new
            {
                status = true
            });
        }
        [HttpPost]
        public JsonResult Update(string cartModel)
        {
            var jsonCart = new JavaScriptSerializer().Deserialize<List<CartItem>>(cartModel);
            var sessionCart = (List<CartItem>)Session[CartSession];

            foreach (var item in sessionCart)
            {
                var jsonItem = jsonCart.SingleOrDefault(x => x.Product.ID == item.Product.ID);
                if (jsonItem != null)
                {
                    item.Quantity = jsonItem.Quantity;
                }
            }
            Session[CartSession] = sessionCart;
            return Json(new
            {
                status = true
            });
        }
        public ActionResult AddItem(long productId)
        {
            var product = new ProductDao().ViewDetail(productId);
            var cart = Session[CartSession];
            if (cart != null)
            {
                var list = (List<CartItem>)cart;
                if (list.Exists(x => x.Product.ID == productId))
                {

                    foreach (var item in list)
                    {
                        if (item.Product.ID == productId)
                        {
                            item.Quantity += 1;
                        }
                    }
                }
                else
                {
                    //tạo mới đối tượng cart item
                    var item = new CartItem();
                    item.Product = product;
                    item.Quantity = 1;
                    list.Add(item);
                }
                //Gán vào session
                Session[CartSession] = list;
            }
            else
            {
                //tạo mới đối tượng cart item
                var item = new CartItem();
                item.Product = product;
                item.Quantity = 1;
                var list = new List<CartItem>();
                list.Add(item);
                //Gán vào session
                Session[CartSession] = list;
            }
            return RedirectToAction("Index");
        }

        [ChildActionOnly]
        public ActionResult Cart()
        {
            var cart = Session[CartSession];
            var list = new List<CartItem>();
            if (cart != null)
            {
                list = (List<CartItem>)cart;
            }
            return PartialView(list);
        }

        [ChildActionOnly]
        public ActionResult InFoCart()
        {
            var cart = Session[CartSession];
            var list = new List<CartItem>();
            if (cart != null)
            {
                list = (List<CartItem>)cart;
            }
            return PartialView(list);
        }
        [HttpGet]
        public ActionResult Payment()
        {
            var cart = Session[CartSession];
            var list = new List<CartItem>();
            if (cart != null)
            {
                list = (List<CartItem>)cart;
            }
            return View(list);
        }
        [HttpPost]
        public ActionResult Payment(string shipName, string mobile, string address, string email)
        {
            var order = new ORDER();
            order.CretedDate = DateTime.Now;
            order.ShipAddress = address;
            order.ShipMobile = mobile;
            order.ShipName = shipName;
            order.ShipEmail = email;

            try
            {
                var id = new OrderDao().Insert(order);
                var cart = (List<CartItem>)Session[CartSession];
                var detailDao = new Model.DAO.OrderDetailDao();
                decimal total = 0;
                foreach (var item in cart)
                {
                    var orderDetail = new ORDERDETAIL();
                    orderDetail.ProductID = item.Product.ID;
                    orderDetail.OrderID = id;
                    orderDetail.Price = item.Product.Price;
                    orderDetail.Quantity = item.Quantity;
                    detailDao.Insert(orderDetail);

                    total += (item.Product.Price.GetValueOrDefault(0) * item.Quantity);
                }
                string content = System.IO.File.ReadAllText(Server.MapPath("~/Areas/Client/template/neworder.html"));

                content = content.Replace("{{CustomerName}}", shipName);
                content = content.Replace("{{Phone}}", mobile);
                content = content.Replace("{{Email}}", email);
                content = content.Replace("{{Address}}", address);
                content = content.Replace("{{Total}}", total.ToString("N0"));
                var toEmail = ConfigurationManager.AppSettings["ToEmailAddress"].ToString();

                new MailHelper().SendMail(email, "Đơn hàng mới từ Basic", content);
                new MailHelper().SendMail(toEmail, "Đơn hàng mới từ Basic", content);
            }
            catch (Exception)
            {
                //ghi log
                return Redirect("/loi-thanh-toan.html");
            }
            return Redirect("/hoan-thanh.html");
        }

        public ActionResult Success()
        {
            return View();
        }

        public JsonResult GetUser()
        {
            var model = (AccountLogin)Session[CommonConstants.AccountSession];
            if (model == null)
            {
                return Json(new
                {
                    status = false
                });
            }
            return Json(new
            {
                data = model,
                status = true
            });
        }
        [HttpPost]
        public JsonResult CreateOrder(string orderViewModel)
        {
            var order = new JavaScriptSerializer().Deserialize<OrderViewModel>(orderViewModel);
            var orderDao = new OrderDao();
            var orderNew = new ORDER();
            orderNew.CretedDate = DateTime.Now;
            orderNew.ShipAddress = order.CustomerAddress;
            orderNew.ShipMobile = order.CustomerMobile;
            orderNew.ShipName = order.CustomerName;
            orderNew.ShipEmail = order.CustomerEmail;
            orderNew.CustomerMessage = order.CustomerMessage;
            orderNew.PaymentMethod = order.PaymentMethod;
            orderNew.PaymentStatus = order.PaymentStatus;
            orderNew.Status = true;
            orderDao.Insert(orderNew);
            var detailDao = new OrderDetailDao();
            var sessionCart = (List<CartItem>)Session[CommonConstants.CartSession];
            decimal total = 0;
            foreach (var item in sessionCart)
            {
                var detail = new ORDERDETAIL();
                detail.OrderID = orderNew.ID;
                detail.ProductID = item.Product.ID;
                detail.Quantity = item.Quantity;
                if (item.Product.PromotionPrice.HasValue)
                {
                    detail.Price = item.Product.PromotionPrice.Value;
                    total += (item.Product.PromotionPrice.GetValueOrDefault(0) * item.Quantity);
                }
                else
                {
                    detail.Price = item.Product.Price;
                    total += (item.Product.Price.Value * item.Quantity);
                }
                detailDao.Insert(detail);
            }

            string content = System.IO.File.ReadAllText(Server.MapPath("~/Areas/Client/template/neworder.html"));
            content = content.Replace("{{CustomerName}}", order.CustomerName);
            content = content.Replace("{{Phone}}", order.CustomerMobile);
            content = content.Replace("{{Email}}", order.CustomerEmail);
            content = content.Replace("{{Address}}", order.CustomerAddress);
            content = content.Replace("{{Total}}", total.ToString("N0"));
            new MailHelper().SendMail(order.CustomerEmail, "Thông tin đơn đặt hàng từ T&Q", content);
            Session[CommonConstants.CartSession] = null;
            return Json(new
            {
                status = true
            });
        }
    }
}