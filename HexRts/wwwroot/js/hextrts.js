var knownTiles = ["water", "desert", "grass", "forest", "mountains", "cultivation", "swamp", "monument"];
var roadSprites = [null, 2, 3, 4, null, 5, 6, 7];
var knownBuildings = ["", "house", "city", "lodge"];
var steps = [0, 1, 0, 2];

var required = [
    { name: "house", resources: [{ name: "Bricks", value: 20 }] },
    { name: "city", resources: [{ name: "Bricks", value: 20 }] },
    { name: "cultivation", resources: [{ name: "Wood", value: 10 }] },
    { name: "lodge", resources: [{ name: "Wood", value: 20 }] },
    { name: "road", resources: [{ name: "Bricks", value: 10 }] },
    { name: "person", resources: [{ name: "Food", value: 50 }] },
    { name: "monument", resources: [{ name: "Wood", value: 100 }, { name: "Iron", value: 50 }, { name: "Food", value: 100 }] },
];

var connection = null;
var gameGrid;
var buildingGrid;
var roadGrid;
var people = [];
var buildings = [];
var selectedTile = null;
var currentPlayer = 0;
var resources = [
    { name: "Wood", value: 0 },
    { name: "Bricks", value: 40 },
    { name: "Food", value: 80 },
    { name: "Iron", value: 0 },
    { name: "People", value: 1 },
    { name: "DayTime", value: 0 },
    { name: "Food/Hour", value: 10 }]
var sheet;
var workers;
var mouseX = null;
var mouseY = null;
var cellX = null;
var cellY = null;
var personHover = null;
var personSelected = null;
var menuEntries = [];
var menuX = 0;
var menuY = 0;
var menuWidth = 0;
var menuItemSelected = null;
var toBuild = null;
var hasHouse = false;
var hasCity = false;
var houseComplete = false;

function CanBuild(what)
{
    var r = null;
    for (var i = 0; i < required.length; i++)
    {
        if (what == required[i].name)
        {
            r = required[i];
            break;
        }
    }
    if (r == null)
        return true;

    for (var i = 0; i < r.resources.length; i++)
    {
        if (getResource(r.resources[i].name) < r.resources[i].value)
            return false;
    }

    if (what == "person" && !((hasHouse || hasCity) && houseComplete))
        return false;
    return true;
}

function CheckMenu()
{
    $("#hexTraderButtons > div").each(function (idx, elem)
    {
        var what = elem.getAttribute("build");
        if (CanBuild(what))
            $(elem).removeClass("hexMenuDisabled");
        else
            $(elem).addClass("hexMenuDisabled");
    });
}

function InitGrid()
{
    gameGrid = [];
    buildingGrid = [];
    roadGrid = [];

    for (var i = 0; i < 14; i++)
    {
        var col = [];
        var roadCol = [];
        var buildingCol = [];
        for (var j = 0; j < 14; j++)
        {
            col.push(0);
            roadCol.push(0);
            buildingCol.push(0);
        }
        gameGrid.push(col);
        roadGrid.push(roadCol);
        buildingGrid.push(buildingCol);
    }
}
function GameClick(evt)
{
    if (cellX === null)
        return;

    if (menuItemSelected !== null)
    {
        menuEntries[menuItemSelected].action();
        return;
    }
    else if (menuEntries.length > 0)
    {
        menuItemSelected = null;
        menuEntries = [];
        return;
    }

    if (toBuild != null)
    {
        $(".selectedHexMenu").removeClass("selectedHexMenu");
        switch (toBuild)
        {
            case "road":
                if (gameGrid[cellX][cellY] > 0 && roadGrid[cellX][cellY] == 0 && gameGrid[cellX][cellY] != 4) // We can build a road
                    Build("road");
                break;
            case "house":
                if ((gameGrid[cellX][cellY] == 1 || gameGrid[cellX][cellY] == 2) && buildingGrid[cellX][cellY] == 0) // We can build an house
                    Build("house");
                break;
            case "cultivation":
                if (gameGrid[cellX][cellY] == 2 && buildingGrid[cellX][cellY] == 0)
                    Build("cultivation");
                break;
            case "lodge":
                if ((gameGrid[cellX][cellY] == 3 || gameGrid[cellX][cellY] == 4 || gameGrid[cellX][cellY] == 6) && buildingGrid[cellX][cellY] == 0)
                    Build("lodge");
                break;
            case "monument":
                if (gameGrid[cellX][cellY] == 2 && buildingGrid[cellX][cellY] == 0)
                    Build("monument");
            default:
                break;
        }
        toBuild = null;
        return;
    }

    menuItemSelected = null;
    menuEntries = [];
    if (personHover !== null && personSelected === null)
    {
        for (var i = 0; i < people.length; i++)
        {
            if (people[i].id == personHover)
            {
                if (people[i].player == currentPlayer) // Can select only somebody you own
                    personSelected = personHover;
                return;
            }
        }
        return;
    }
    if (personSelected !== null)
    {
        menuEntries.push({ label: "Walk Here", action: PersonPlace });
        if (personHover !== null && personHover !== personSelected) // maybe over an opponent?
        {
            var overOpponent = false;
            for (var i = 0; i < people.length; i++)
            {
                if (people[i].id == personHover)
                {
                    overOpponent = (people[i].player != currentPlayer)
                    break;
                }
            }

            if (overOpponent)
                menuEntries.push({ label: "Attack", action: AttackPerson });
        }

        var b = null;
        for (var i = 0; i < buildings.length; i++)
        {
            if (buildings[i].x == cellX && buildings[i].y == cellY)
            {
                b = buildings[i];
                break;
            }
        }
        if (b !== null && b.player == currentPlayer && b.state == 0)
            menuEntries.push({ label: "Assign to construction", action: PersonAssignConstruction });
        if (b !== null && b.player == currentPlayer && b.state != 0 && buildingGrid[cellX][cellY] != 1 && buildingGrid[cellX][cellY] != 2 && gameGrid[cellX][cellY] != 7)
            menuEntries.push({ label: "Work here", action: PersonAssignWorker });
        menuEntries.push({ label: "Set Auto", action: PersonAuto });
        menuEntries.push({ label: "Deselect People", action: PersonDeselect });
    }
    if (buildingGrid[cellX][cellY] == 1 || buildingGrid[cellX][cellY] == 2) // We can generate a person
    {
        menuEntries.push({ label: "Spawn Worker (food: 50)", action: function () { Build("person"); }, canBuild: CanBuild("person") });
    }
    if (gameGrid[cellX][cellY] > 0 && roadGrid[cellX][cellY] == 0 && gameGrid[cellX][cellY] != 4) // We can build a road
    {
        menuEntries.push({ label: "Build Road (bricks: 10)", action: function () { Build("road"); }, canBuild: CanBuild("road") });
    }
    if ((gameGrid[cellX][cellY] == 1 || gameGrid[cellX][cellY] == 2) && buildingGrid[cellX][cellY] == 0) // We can build an house
    {
        menuEntries.push({ label: "Build House (bricks: 20)", action: function () { Build("house"); }, canBuild: CanBuild("house") });
    }
    if ((gameGrid[cellX][cellY] == 1 || gameGrid[cellX][cellY] == 2) && buildingGrid[cellX][cellY] == 1) // We can build a city
    {
        menuEntries.push({ label: "Build City (bricks: 60)", action: function () { Build("city"); }, canBuild: CanBuild("city") });
    }
    if (gameGrid[cellX][cellY] == 2 && buildingGrid[cellX][cellY] == 0) // We can cultivate
    {
        menuEntries.push({ label: "Cultivate (wood: 10)", action: function () { Build("cultivation"); }, canBuild: CanBuild("cultivation") });
    }
    if ((gameGrid[cellX][cellY] == 3 || gameGrid[cellX][cellY] == 4 || gameGrid[cellX][cellY] == 6) && buildingGrid[cellX][cellY] == 0) // We can build a lodge
    {
        menuEntries.push({ label: "Build Lodge (wood: 20)", action: function () { Build("lodge"); }, canBuild: CanBuild("lodge") });
    }
    if (gameGrid[cellX][cellY] == 2 && buildingGrid[cellX][cellY] == 0) // We can build a monument
    {
        menuEntries.push({ label: "Monument (wood: 100, iron: 50, food: 100)", action: function () { Build("monument"); }, canBuild: CanBuild("monument") });
    }

    if (menuEntries.length > 0)
    {
        var ctx = document.getElementById("gameCanvas").getContext("2d");
        ctx.font = "30px sans-serif";
        menuX = cellX * 130 + (cellY % 2) * 65 + 65;
        menuY = cellY * 90 + 45;
        menuWidth = 0;
        for (var i = 0; i < menuEntries.length; i++)
            menuWidth = Math.max(menuWidth, ctx.measureText(menuEntries[i].label).width + 20);
        if (menuX + menuWidth + 10 > 1650)
            menuX = 1650 - (menuWidth + 10);
        if (menuY + menuEntries.length * 40 + 5 > 1250)
            menuY = 1250 - (menuEntries.length * 40 + 5);
    }
}

function PersonAuto()
{
    HideActionMenu();

    connection.invoke("PersonAuto", personSelected).then(function ()
    {
    }).catch(function (err2)
    {
        return console.error(err2.toString());
    });
}

function PersonAssignConstruction()
{
    HideActionMenu();

    connection.invoke("AssignPersonConstruction", personSelected, cellX, cellY).then(function ()
    {
    }).catch(function (err2)
    {
        return console.error(err2.toString());
    });
}

function PersonAssignWorker()
{
    HideActionMenu();

    connection.invoke("AssignPersonWorker", personSelected, cellX, cellY).then(function ()
    {
    }).catch(function (err2)
    {
        return console.error(err2.toString());
    });
}

function PersonPlace()
{
    HideActionMenu();

    connection.invoke("SetPersonDestination", personSelected, cellX, cellY).then(function ()
    {
    }).catch(function (err2)
    {
        return console.error(err2.toString());
    });
}

function PersonDeselect()
{
    HideActionMenu();
    personSelected = null;
}

function HideActionMenu()
{
    menuEntries = [];
    menuItemSelected = null;
}

function Build(what)
{
    HideActionMenu();

    connection.invoke("Build", what, cellX, cellY).then(function ()
    {
    }).catch(function (err2)
    {
        return console.error(err2.toString());
    });
}

function CheckRoad(x, y)
{
    if (x < 0 || x > 13 || y < 0 || y > 13)
        return 0;
    return roadGrid[x][y] != 0;
}

function Draw()
{
    var ctx = document.getElementById("gameCanvas").getContext("2d");
    var w = 1950;
    ctx.fillStyle = "#000000";
    ctx.fillRect(0, 0, w, w);

    // Draw the tiles
    for (var j = -1; j < 14; j++)
    {
        for (var i = -1; i < 14; i++)
        {
            if (i < 0 || i > 12 || j < 0 || j > 12)
                ctx.drawImage(sheet, 0, 0, 150, 150, i * 130 + (j % 2) * 65, j * 90, 150, 150);
            else
            {
                ctx.drawImage(sheet, gameGrid[i][j] * 150, 0, 150, 150, i * 130 + (j % 2) * 65, j * 90, 150, 150);

                // Draw the roads and possibly connect them with other tiles
                // Uses the correct sprite depending on the base tile under
                if (roadGrid[i][j] != 0 && roadSprites[gameGrid[i][j]] !== null)
                {
                    if (CheckRoad(i - 1, j))
                        ctx.drawImage(sheet, 2 * 150, roadSprites[gameGrid[i][j]] * 150, 150, 150, i * 130 + (j % 2) * 65, j * 90, 150, 150);
                    if (CheckRoad(i + 1, j))
                        ctx.drawImage(sheet, 5 * 150, roadSprites[gameGrid[i][j]] * 150, 150, 150, i * 130 + (j % 2) * 65, j * 90, 150, 150);
                    var x = i - (j % 2 == 0 ? 1 : 0);
                    if (CheckRoad(x, j - 1))
                        ctx.drawImage(sheet, 1 * 150, roadSprites[gameGrid[i][j]] * 150, 150, 150, i * 130 + (j % 2) * 65, j * 90, 150, 150);
                    if (CheckRoad(x, j + 1))
                        ctx.drawImage(sheet, 3 * 150, roadSprites[gameGrid[i][j]] * 150, 150, 150, i * 130 + (j % 2) * 65, j * 90, 150, 150);
                    if (CheckRoad(x + 1, j + 1))
                        ctx.drawImage(sheet, 4 * 150, roadSprites[gameGrid[i][j]] * 150, 150, 150, i * 130 + (j % 2) * 65, j * 90, 150, 150);
                    if (CheckRoad(x + 1, j - 1))
                        ctx.drawImage(sheet, 0 * 150, roadSprites[gameGrid[i][j]] * 150, 150, 150, i * 130 + (j % 2) * 65, j * 90, 150, 150);
                    if (!CheckRoad(x, j - 1) && !CheckRoad(i - 1, j) && !CheckRoad(x, j + 1) && !CheckRoad(x + 1, j + 1) && !CheckRoad(i + 1, j) && !CheckRoad(x + 1, j - 1))
                        ctx.drawImage(sheet, 3 * 150, roadSprites[gameGrid[i][j]] * 150, 150, 150, i * 130 + (j % 2) * 65, j * 90, 150, 150);
                }
            }
        }
    }

    // Draw the buildings sprites
    for (var j = 0; j < 13; j++)
        for (var i = 0; i < 13; i++)
            if (buildingGrid[i][j] > 0)
                ctx.drawImage(sheet, (buildingGrid[i][j] - 1) * 150, 150, 150, 150, i * 130 + (j % 2) * 65, j * 90, 150, 150);

    // Draw the building progress bar
    ctx.lineWidth = 1;
    for (var i = 0; i < buildings.length; i++)
    {
        // Building flag
        ctx.drawImage(sheet, (buildings[i].player + 3) * 150, 150, 150, 150, buildings[i].x * 130 + (buildings[i].y % 2) * 65, buildings[i].y * 90, 150, 150);
        if (buildings[i].player == currentPlayer)
        {
            var bp = BuildingProcent(buildings[i]);
            if (bp != 1)
            {
                ctx.fillStyle = "rgba(0,0,0,0.6)";
                ctx.strokeStyle = "#FFFFFF";
                ctx.fillRect(buildings[i].x * 130 + (buildings[i].y % 2) * 65 - 0.5, buildings[i].y * 90 - 0.5, 132, 12);
                ctx.strokeRect(buildings[i].x * 130 + (buildings[i].y % 2) * 65 + 0.5, buildings[i].y * 90 + 0.5, 130, 10);
                ctx.fillStyle = "#FFFFFF";
                ctx.fillRect(buildings[i].x * 130 + (buildings[i].y % 2) * 65 + 2.5, buildings[i].y * 90 + 2.5, 126 * bp, 6);
            }
        }
    }

    // Draw the workers
    ctx.lineWidth = 1;
    for (var i = 0; i < people.length; i++)
    {
        /*var x = people[i].x * 5 - 15;
        var y = people[i].y * 4 + 25;*/

        var x = people[i].x - 15;
        var y = people[i].y + 10;

        var nbInSameCell = PeopleInCell(people[i]);
        if (nbInSameCell > 0)
        {
            x += Math.cos(i * (Math.PI / 3)) * 25;
            y += Math.sin(i * (Math.PI / 3)) * 25;
        }

        if (personHover !== null && personHover == people[i].id)
        {
            ctx.fillStyle = "rgba(255,204,0,0.5)";
            ctx.strokeStyle = "#ffffff";
            ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.ellipse(x + 25, y + 25, 25, 25, Math.PI * 2, 0, Math.PI * 2);
            ctx.fill();
            ctx.stroke();
        }
        else if (personSelected !== null && personSelected == people[i].id)
        {
            ctx.fillStyle = "rgba(255,0,0,0.5)";
            ctx.strokeStyle = "#ffffff";
            ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.ellipse(x + 25, y + 25, 25, 25, Math.PI * 2, 0, Math.PI * 2);
            ctx.fill();
            ctx.stroke();
        }
        var s = 0;
        if (people[i].dir !== null && people[i].dir !== undefined)
            s = people[i].dir * 3 + steps[people[i].step];
        if (people[i].dx == 0 && people[i].dy == 0 && (people[i].task == 1 || people[i].task == 2))
            s += 18;
        else if (people[i].task == 3 && people[i].transporting)
            s += 36;
        ctx.drawImage(workers, s * 50, people[i].player * 50, 50, 50, x, y, 50, 50);

        var ph = Math.min(1, people[i].life / people[i].maxLife);
        if (ph != 1)
        {
            x -= 8;
            y += 50;
            ctx.fillStyle = "rgba(0,0,0,0.6)";
            ctx.fillRect(x - 1.5, y - 1.5, 67, 8);
            ctx.fillStyle = "#FF0000";
            ctx.fillRect(x + 0.5, y + 0.5, 65 * ph, 6);
        }
    }

    /*ctx.fillStyle = "#FF0000";
    ctx.font = "20px sans-serif";
    // Draw the coordinates
    for (var j = 0; j < 13; j++)
        for (var i = 0; i < 13; i++)
            ctx.fillText("" + i + ", " + j, i * 130 + (j % 2) * 65, j * 90);*/

    // Mouse over a cell
    if (cellX !== null && personHover === null)
    {
        // Draw the current cell
        ctx.strokeStyle = "#FFFFFF";
        ctx.fillStyle = "rgba(255,204,0,0.5)";
        ctx.lineWidth = 3;
        ctx.beginPath();
        var ox = cellX * 130 + (cellY % 2) * 65;
        var oy = cellY * 90;
        ctx.moveTo(74 + ox, 15 + oy);
        ctx.lineTo(140 + ox, 50 + oy);
        ctx.lineTo(140 + ox, 105 + oy);
        ctx.lineTo(74 + ox, 140 + oy);
        ctx.lineTo(10 + ox, 105 + oy);
        ctx.lineTo(10 + ox, 50 + oy);
        ctx.closePath();
        ctx.stroke();
        ctx.fill();
    }

    // Resources
    ctx.fillStyle = "rgba(0,0,0,0.5)";
    ctx.fillRect(0, 0, 1650, 60);
    ctx.fillStyle = "#ffffff";
    ctx.strokeStyle = "#000000";
    ctx.lineWidth = 5;
    ctx.font = "40px sans-serif";
    for (var i = 0; i < resources.length; i++)
    {
        ctx.strokeText(resources[i].name + ": " + resources[i].value, i * 320 + 20, 40);
        ctx.fillText(resources[i].name + ": " + resources[i].value, i * 320 + 20, 40);
    }

    // Menu
    if (menuEntries.length > 0)
    {
        menuItemSelected = null;
        ctx.font = "30px sans-serif";
        ctx.fillStyle = "rgba(0,0,0,0.5)";
        ctx.fillRect(menuX, menuY, menuWidth, menuEntries.length * 40 + 5);
        ctx.fillStyle = "#ffffff";
        for (var i = 0; i < menuEntries.length; i++)
        {
            if (mouseX > menuX - 10 && mouseX < menuX + menuWidth && mouseY > menuY + i * 40 - 10 && mouseY < menuY + i * 40 + 30)
            {
                if (menuEntries[i].canBuild)
                    ctx.fillStyle = "#00ff00";
                else
                    ctx.fillStyle = "#ff0000";
                menuItemSelected = i;
            }
            else
                ctx.fillStyle = "#ffffff";
            ctx.fillText(menuEntries[i].label, menuX + 10, menuY + i * 40 + 30);
        }
    }
}

function PeopleInCell(person)
{
    var pos = ScreenToCell(person);
    var res = 0;
    for (var i = 0; i < people.length; i++)
    {
        if (person == people[i])
            continue;
        var p = ScreenToCell(people[i]);
        if (p.x == pos.x && p.y == pos.y)
            res++;
    }
    return res;
}

function ScreenToCell(source)
{
    var y = Math.floor(source.y / 90);
    return { x: Math.floor((source.x - (y % 2) * 65) / 130), y: y };
}

function SetBuildingProgress(building)
{
    if (building.player != currentPlayer)
        return;
    var bg = document.getElementById("building_" + building.id);
    var bp = BuildingProcent(building);
    if (bp == 1)
        bg.setAttribute("visibility", "hidden");
    else
    {
        bg.setAttribute("visibility", "visible");
        bg.children[1].setAttribute("width", bp * 22);
    }
}

function BuildingProcent(building)
{
    if (building.state == 1)
        return 1;
    else if (building.state == 2)
        return Math.min(1, building.taskInProgress / building.productionTime);
    else
        return Math.min(1, building.taskInProgress / building.constructionTime);
}

function HasPersonSelected()
{
    var selection = document.getElementById("gameWorld").querySelectorAll(".personSelected");
    return selection.length > 0;
}

function AttackPerson()
{
    HideActionMenu();

    connection.invoke("AttackPerson", personSelected, personHover).then(function ()
    {
    }).catch(function (err2)
    {
        return console.error(err2.toString());
    });
}

function KeyDown(evt)
{
    switch (evt.keyCode)
    {
        case 27:
            toBuild = null;
            $(".selectedHexMenu").removeClass("selectedHexMenu");
            HideActionMenu();
            personSelected = null;
            break;
        default:
            break;
    }
}

function getResource(name)
{
    for (var i = 0; i < resources.length; i++)
    {
        if (resources[i].name.toLowerCase() == name.toLowerCase())
            return resources[i].value;
    }
    return null;
}

function setResource(name, value)
{
    for (var i = 0; i < resources.length; i++)
    {
        if (resources[i].name.toLowerCase() == name)
        {
            resources[i].value = value;
            var d = document.getElementById(resources[i].name.toLowerCase() + "Display");
            d.textContent = resources[i].name + ": " + value;
            return;
        }
    }
}

function InitResources()
{
    var res = document.getElementById("resourcesContainer");
    Clear(res);

    var xPos = 0;
    var yPos = 8;
    for (var i = 0; i < resources.length; i++)
    {
        var r = Add("text", {
            x: (xPos == 0 ? 8 : xPos),
            y: yPos
        }, resources[i].name + ": " + resources[i].value, res);
        r.setAttribute("id", resources[i].name.toLowerCase() + "Display");
        xPos += 80;
        if (xPos > 240)
        {
            xPos = 0;
            yPos += 14;
        }
    }
}

function DeserializeGrid(gridString)
{
    var result = [];
    var pos = 0;
    for (var i = 0; i < 14; i++)
    {
        var col = [];
        for (var j = 0; j < 14; j++, pos++)
            col.push(gridString.charCodeAt(pos) - 65);
        result.push(col);
    }
    return result;
}

function HexTraderResize()
{
    var w = $(window).innerWidth() - 140;
    var h = $(window).innerHeight();

    var fw = w / 1650;
    var fh = h / 1250;

    factor = Math.min(fw, fh);

    $("#gameCanvas").width(factor * 1650).height(factor * 1250).css({ top: ((h / 2) - (factor * 1250 / 2)) + "px", left: ((w / 2) + 130 - (factor * 1650 / 2)) + "px" });
}

function MouseMove(evt)
{
    var w = $(window).innerWidth() - 140;
    var h = $(window).innerHeight();

    var fw = w / 1650;
    var fh = h / 1250;
    factor = Math.min(fw, fh);

    if (evt.touches) // touch event
    {
        mouseX = (evt.touches[0].pageX - $("#gameCanvas").first().position().left) / factor;
        mouseY = (evt.touches[0].pageY - $("#gameCanvas").first().position().top) / factor;
    }
    else
    {
        mouseX = evt.offsetX / factor;
        mouseY = evt.offsetY / factor;
    }

    if (menuEntries.length > 0)
        return;

    personHover = null;
    for (var i = 0; i < people.length; i++)
    {
        var x = people[i].x - 15;
        var y = people[i].y + 10;

        var nbInSameCell = PeopleInCell(people[i]);
        if (nbInSameCell > 0)
        {
            x += Math.cos(i * (Math.PI / 3)) * 25;
            y += Math.sin(i * (Math.PI / 3)) * 25;
        }

        if (mouseX >= x && mouseX <= x + 50 && mouseY >= y && mouseY <= y + 50) // Hover a person
            personHover = people[i].id;
    }

    // Find the general position
    cellY = Math.floor(mouseY / 90);
    cellX = Math.floor((mouseX - (cellY % 2) * 65) / 130);

    // Center of the cell
    var cx = cellX * 130 + (cellY % 2) * 65 + 65;
    var cy = cellY * 90 + 45;

    var dx = mouseX - cx;
    var dy = mouseY - cy;

    if (dy < -5) // Top hat
    {
        var y = 40 - (dy + 45);
        var x = 65 - y * 65 / 40;
        if (cellY % 2 == 0)
        {
            if (dx < 0 && dx < -x)
            {
                cellY--;
                cellX--;
            }
            else if (dx > 0 && dx > x)
                cellY--;
        }
        else
        {
            if (dx < 0 && dx < -x)
                cellY--;
            else if (dx > 0 && dx > x)
            {
                cellX++;
                cellY--;
            }
        }
    }
}

function MenuClick(evt)
{
    $(".selectedHexMenu").removeClass("selectedHexMenu");

    var elem = this;
    if (elem.getAttribute("build") == toBuild)
    {
        toBuild = null;
        return;
    }
    toBuild = elem.getAttribute("build");
    if (!CanBuild(toBuild))
        return;
    if (toBuild == "city")
    {
        for (var i = 0; i < buildings.length; i++)
        {
            if (buildings[i].player == currentPlayer && buildingGrid[buildings[i].x][buildings[i].y] == 1) // The player house
            {
                connection.invoke("Build", "city", buildings[i].x, buildings[i].y).then(function ()
                {
                }).catch(function (err2)
                {
                    return console.error(err2.toString());
                });
                toBuild = null;
                return;
            }
        }
    }
    else if (toBuild == "person")
    {
        for (var i = 0; i < buildings.length; i++)
        {
            if (buildings[i].player == currentPlayer && (buildingGrid[buildings[i].x][buildings[i].y] == 1 || buildingGrid[buildings[i].x][buildings[i].y] == 2)) // The player house
            {
                connection.invoke("Build", "person", buildings[i].x, buildings[i].y).then(function ()
                {
                }).catch(function (err2)
                {
                    return console.error(err2.toString());
                });
                toBuild = null;
                return;
            }
        }
    }
    else if (toBuild == "?")
    {
        toBuild = null;
        $("#gameHelp").show();
        return;
    }
    else
        $(this).addClass("selectedHexMenu");
}

function Init()
{
    sheet = new Image();
    sheet.src = "/images/hexrts/sheet.png?v=2";
    workers = new Image();
    workers.src = "/images/hexrts/workers.png?v=2";

    InitGrid();
    $("#gameHelp > div:nth-child(2) > span").on("click", Start);
    if (window.localStorage.getItem("hexHelp"))
        Start();
}

function Start()
{
    $("#gameHelp").hide();
    $("#hexGame").show().css({ position: "fixed" });
    if (connection)
        return;
    window.localStorage.setItem("hexHelp", true);
    setInterval(Draw, 50);

    connection = new signalR.HubConnectionBuilder().withUrl("/hubs/hexrts").build();

    connection.on("GameGrid", function (g)
    {
        gameGrid = DeserializeGrid(g);
    });
    connection.on("RoadGrid", function (g)
    {
        roadGrid = DeserializeGrid(g);
    });
    connection.on("BuildingGrid", function (g)
    {
        buildingGrid = DeserializeGrid(g);
    });
    connection.on("People", function (p)
    {
        for (var i = 0; i < p.length; i++)
        {
            for (var j = 0; j < people.length; j++)
            {
                if (p[i].id == people[j].id) // Same person
                {
                    var dx = p[i].x - people[j].x;
                    var dy = p[i].y - people[j].y;

                    // Get back the previous direction
                    if (people[j].dir === null || people[j].dir === undefined)
                        p[i].dir = 0;
                    else
                        p[i].dir = people[j].dir;

                    if (dx > 0 && dy > 0)
                        p[i].dir = 0;
                    else if (dx > 0 && dy == 0)
                        p[i].dir = 1;
                    else if (dx > 0 && dy < 0)
                        p[i].dir = 2;
                    else if (dx == 0 && dy < 0)
                        p[i].dir = 3;
                    else if (dx < 0 && dy < 0)
                        p[i].dir = 3;
                    else if (dx < 0 && dy == 0)
                        p[i].dir = 4;
                    else if (dx < 0 && dy > 0)
                        p[i].dir = 5;
                    else if (dx == 0 && dy > 0)
                        p[i].dir = 0;

                    p[i].step = 0;
                    if (people[i].step !== null && people[i].step !== undefined)
                        p[i].step = people[i].step;

                    p[i].dx = dx;
                    p[i].dy = dy;
                    if (dx != 0 || dy != 0)
                        p[i].step = (p[i].step + 1) % 4;
                    else if (p[i].task == people[i].task && (p[i].task == 1 || p[i].task == 2))
                        p[i].step = (p[i].step + 1) % 4;
                    else
                        p[i].step = 0;
                    break;
                }
            }
        }
        people = p;
    });
    connection.on("Buildings", function (b)
    {
        hasHouse = false;
        hasCity = false;
        houseComplete = false;
        for (var i = 0; i < b.length; i++)
        {
            if (b[i].player == currentPlayer && buildingGrid[b[i].x][b[i].y] == 1)
            {
                hasHouse = true;
                houseComplete = (BuildingProcent(b[i]) == 1);
            }
            if (b[i].player == currentPlayer && buildingGrid[b[i].x][b[i].y] == 2)
            {
                hasCity = true;
                houseComplete = (BuildingProcent(b[i]) == 1);
            }
        }
        if (hasHouse || hasCity)
        {
            $("#hexHouse").hide();
        }
        if (hasHouse && !hasCity && houseComplete)
            $("#hexCity").show();
        if (hasCity)
            $("#hexCity").hide();
        if (currentPlayer == 0)
        {
            $("#hexRed").show();
            $("#hexBlue").hide();
        }
        else
        {
            $("#hexBlue").show();
            $("#hexRed").hide();
        }
        buildings = b;
    });
    connection.on("Resources", function (res)
    {
        resources = res;
        CheckMenu();
    });
    connection.on("PlayerColor", function (c)
    {
        currentPlayer = c;
        if (currentPlayer == 0)
        {
            $("#hexRed").show();
            $("#hexBlue").hide();
        }
        else
        {
            $("#hexBlue").show();
            $("#hexRed").hide();
        }
        CheckMenu();
    });
    connection.on("End", function ()
    {
        $("#gameResultContainer").show();
        $("#gameResult").html("Game finished");
    });
    connection.on("Won", function ()
    {
        $("#gameResultContainer").show();
        $("#gameResult").html("You won!");
    });
    connection.on("Lost", function ()
    {
        $("#gameResultContainer").show();
        $("#gameResult").html("You lost...");
    });
    connection.on("Tie", function ()
    {
        $("#gameResultContainer").show();
        $("#gameResult").html("It's a tie!");
    });
    connection.on("Playing", function ()
    {
        $("#gameResultContainer").hide();
    });

    connection.start().then(function ()
    {
        connection.invoke("Init").catch(function (err2)
        {
            return console.error(err2.toString());
        });

        $("#connection").addClass("connected");
    }).catch(function (err2)
    {
        return console.error(err2.toString());
    });

    $("#hexBlue").hide();
    $("#hexRed").hide();
    $("#hexTraderButtons > div").on("click", MenuClick);
    $("#hexCity").hide();

    $(document).on("keydown", KeyDown);
    $("#gameCanvas").on("mousemove", MouseMove).on("mouseup", GameClick).on("touchmove", MouseMove).on("touchend", GameClick).on("contextmenu", function (evt)
    {
        toBuild = null;
        MouseMove(evt);
        evt.preventDefault();
        return false
    });
    HexTraderResize();
}



$(window).resize(HexTraderResize);
$(Init);