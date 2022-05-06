# spine-optimize
Spine for Unity Version 2.1

First：
Convert JSON to binary.

Second:
Use pointer movement instead of Stream read.

Once again, to optimize:
All strings moved to the header, using index instead.
Delete nonessential data.
Merge Timeline.
So the binary 50%-70% smaller.

spine版本比较老2.1，但优化原理一样。
第一步：读取json格式转成二进制。
第二步：使用指针代替Stream读取。
第三步：将所有的string移到头部，使用下标代替，合并相同的timeline。
这样可以使二进制再小50%-70%。