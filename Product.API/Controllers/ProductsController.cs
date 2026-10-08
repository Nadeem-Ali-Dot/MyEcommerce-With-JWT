using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Product.API.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        // 1) PUBLIC (No Auth Required)
        // GET api/products/public
        [HttpGet("public")]
        [AllowAnonymous]
        public IActionResult GetPublicCatalog()
        {
            return Ok("Public product catalog (No JWT required).");
        }

        // 2) AUTHENTICATION ONLY
        //    Demonstrates: User must send a valid JWT.
        // GET api/products
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult GetAllProducts()
        {
            return Ok("All products (JWT required - Authentication only).");
        }

        // 3) POLICY-BASED AUTHORIZATION
        //    Demonstrates: Policy name from JwtAuth.Shared
        //    Example: CustomerOnly
        // GET api/products/{productId}
        // Example: GET api/products/3fa85f64-5717-4562-b3fc-2c963f66afa6
        [HttpGet("{productId:guid}")]
        [Authorize(AuthenticationSchemes = "Bearer", Policy = "CustomerOnly")]
        public IActionResult GetProductById(Guid productId)
        {
            return Ok($"Product details for {productId} (Policy: CustomerOnly).");
        }

        // 4) ROLE-BASED AUTHORIZATION
        //    Demonstrates: Role check using Roles property
        //    NOTE: This bypasses policy and shows direct role-based auth.
        // POST api/products
        [HttpPost]
        [Authorize(Roles = "Admin,Seller")]
        public IActionResult CreateProduct()
        {
            return Ok("Product created (Roles: Admin OR Seller).");
        }

        // 5) POLICY-BASED AUTHORIZATION
        //    Demonstrates: Policy name from JwtAuth.Shared
        //    Example: AdminOrSeller
        // PUT api/products/{productId}
        // PUT api/products/3fa85f64-5717-4562-b3fc-2c963f66afa6
        [HttpPut("{productId:guid}")]
        [Authorize(Policy = "AdminOrSeller")]
        public IActionResult UpdateProduct(Guid productId)
        {
            return Ok($"Product {productId} updated (Policy: AdminOrSeller).");
        }

        // 6) POLICY-BASED AUTHORIZATION
        //    Demonstrates: Policy name from JwtAuth.Shared
        //    Example: AdminOnly
        // DELETE api/products/{productId}
        // DELETE api/products/3fa85f64-5717-4562-b3fc-2c963f66afa6
        [HttpDelete("{productId:guid}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult DeleteProduct(Guid productId)
        {
            return Ok($"Product {productId} deleted (Policy: AdminOnly).");
        }

        // Auth Diagnostic (Authentication only)
        // Demonstrates:
        // - JWT validation succeeded (HttpContext.User is populated)
        // - Reading claims from the token
        // GET api/products/whoami
        [HttpGet("whoami")]
        [Authorize]
        public IActionResult WhoAmI()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();

            return Ok(new
            {
                Message = "JWT validated successfully.",
                UserId = userId,
                Roles = roles,
                ClientId = User.FindFirstValue("client_id"),
                SessionId = User.FindFirstValue("sid")
            });
        }
    }

}
