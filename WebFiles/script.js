var mySocket;
var params;
var pid;
var data;
var showMenu = true;
function setup() {
    createCanvas(windowWidth, windowHeight);
    params = getURLParams();
    pid = params.pid;
    noLoop();
}

function draw() {
    clear();
    if (data != undefined) {
        for (i = 0; i < data.length; i++) {
            switch (data[i].Type) {
                case "rect":
                    DrawBox(data[i].x, data[i].y, data[i].width, data[i].height, data[i].color, data[i].fill);
                    break;
                case "text":
                    DrawText(data[i].x, data[i].y, data[i].txt, data[i].txtSize, data[i].color);
                    break;
                case "circle":
                    DrawCircle(data[i].x, data[i].y, data[i].d, data[i].color, data[i].fill);
                    break;
                case "line":
                    DrawLine(data[i].x, data[i].y, data[i].x1, data[i].y1, data[i].color);
                    break;
            }
        }
        mySocket.send(pid + "#frame#");
    }
}

function windowResized() {
    resizeCanvas(windowWidth, windowHeight);
    mySocket.send(pid + '#update#' + windowWidth + '#' + windowHeight);
}
function keyPressed() {
    if (keyCode == 45) { // insert key
        showMenu = !showMenu;
    }
}
function DrawCircle(x, y, d, c, f) {
    if (f) { fill(c); } else { noFill(); }
    stroke(c);
    circle(x, y, d);
    noStroke();
}
function DrawText(x, y, txt, txtSize, c) {
    textSize(txtSize);
    fill(c);
    text(txt, x, y);
}
function DrawBox(x, y, w, h, c, f) {
    if (f) { fill(c); } else { noFill(); }
    stroke(c);
    rect(x, y, w, h);
    noStroke();
}
function DrawLine(x1, y1, x2, y2, c) {
    stroke(c);
    line(x1, y1, x2, y2);
    noStroke();
}

const socketMessageListener = (event) => {
    data = JSON.parse(event.data);
    redraw();
};

// Open
const socketOpenListener = (event) => {
    mySocket.send(pid + '#new#' + windowWidth + '#' + windowHeight);
};

// Closed
const socketCloseListener = (event) => {
    if (mySocket) {
        console.error('Disconnected.');
        data = undefined;
    }
    mySocket = new WebSocket('ws://localhost:3030');
    mySocket.addEventListener('open', socketOpenListener);
    mySocket.addEventListener('message', socketMessageListener);
    mySocket.addEventListener('close', socketCloseListener);
};
socketCloseListener();

/*

  DrawText(200,200,'rakan',32,'red');
  DrawCircle(100,100,200,'blue',true);
  DrawBox(100,100,100,100,'yellow',true);

function DrawCircle(x,y,d,c,f)
{
  if(f){fill(c);}else{noFill();}
  stroke(c);
  circle(x,y,d);
  noStroke();
}
function DrawText(x,y,txt,txtSize,c)
{
  textSize(txtSize);
  fill(c);
  text(txt,x,y);
}
function DrawBox(x,y,w,h,c,f)
{
  if(f){fill(c);}else{noFill();}
  stroke(c);
  rect(x,y,w,h);
  noStroke();
}
*/