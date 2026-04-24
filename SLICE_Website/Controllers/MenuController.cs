using Microsoft.AspNetCore.Mvc;
using SLICE_Website.Data;
using SLICE_Website.Models;
using System.Collections.Generic;

namespace SLICE_Website.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MenuController : ControllerBase
    {
        private readonly MenuRepository _menuRepo;

        public MenuController(MenuRepository menuRepo)
        {
            _menuRepo = menuRepo;
        }

        [HttpGet]
        public IActionResult GetAllMenuItems()
        {
            try { return Ok(_menuRepo.GetAllMenuItems()); }
            catch (System.Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpGet("{id}/recipe")]
        public IActionResult GetRecipe(int id)
        {
            try { return Ok(_menuRepo.GetRecipeForProduct(id)); }
            catch (System.Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpPost]
        public IActionResult AddMenu([FromBody] MenuItem item)
        {
            try
            {
                _menuRepo.AddMenuItem(item);
                return Ok(new { Message = "Menu item created successfully!" });
            }
            catch (System.Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpPut]
        public IActionResult UpdateMenu([FromBody] MenuItem item)
        {
            try
            {
                _menuRepo.UpdateMenuItem(item);
                return Ok(new { Message = "Menu item updated successfully!" });
            }
            catch (System.Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteMenu(int id)
        {
            try
            {
                _menuRepo.DeleteMenuItem(id);
                return Ok(new { Message = "Menu item deleted successfully." });
            }
            catch (System.Exception ex) { return StatusCode(500, ex.Message); }
        }
    }
}