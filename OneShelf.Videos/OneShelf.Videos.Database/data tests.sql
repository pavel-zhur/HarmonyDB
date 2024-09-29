-- test 1: no results
select *
from videos.staticmessages m
where 
	(m.photo is not null)
	and (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))

-- test 2: no results
select *
from (
	select staticchatid, id staticmessageid,date
	from videos.staticmessages m
	where 
		(m.photo is not null)
		or (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
) m
left join videos.uploadeditems u on u.staticchatid = m.staticchatid and u.staticmessageid = m.staticmessageid
where u.id is null

-- test 3: no results
select m.*, u.telegrampublishedon, u.status, u.statuscode, u.statusmessage, u.mediaitemid, u.mediaitemmetadatacreationtime, u.filenametimestamp
from (
	select staticchatid, id staticmessageid,date, case when m.photo is not null then 'photo' else 'video' end type
	from videos.staticmessages m
	where 
		(m.photo is not null)
		or (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
) m
left join videos.uploadeditems u on u.staticchatid = m.staticchatid and u.staticmessageid = m.staticmessageid
where m.date <> u.telegrampublishedon

-- test 4: no results
select m.*, u.telegrampublishedon, u.status, u.statuscode, u.statusmessage, u.mediaitemid, u.mediaitemmetadatacreationtime, u.filenametimestamp
from (
	select staticchatid, id staticmessageid,date, case when m.photo is not null then 'photo' else 'video' end type
	from videos.staticmessages m
	where 
		(m.photo is not null)
		or (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
) m
left join videos.uploadeditems u on u.staticchatid = m.staticchatid and u.staticmessageid = m.staticmessageid
where filenametimestamp <> mediaitemmetadatacreationtime

-- test 5: no results
select m.*, u.status, u.statuscode, u.statusmessage, u.mediaitemid, u.mediaitemmetadatacreationtime, 
	datediff(minute, mediaitemmetadatacreationtime, date) minutes, datediff(day, mediaitemmetadatacreationtime, date) days
	, i.filename, i.isphoto, i.isvideo, i.mediametadatacreationtime, i.mediametadatavideostatus, i.producturl
from (
	select staticchatid, id staticmessageid,date, case when m.photo is not null then 'photo' else 'video' end type
	from videos.staticmessages m
	where 
		(m.photo is not null)
		or (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
) m
left join videos.uploadeditems u on u.staticchatid = m.staticchatid and u.staticmessageid = m.staticmessageid
left join videos.inventoryitems i on i.id = u.mediaitemid
where i.mediametadatacreationtime <> mediaitemmetadatacreationtime

-- test 6: no results
select m.*, u.status, u.statuscode, u.statusmessage, u.mediaitemid, u.mediaitemmetadatacreationtime, 
	datediff(minute, mediaitemmetadatacreationtime, date) minutes, datediff(day, mediaitemmetadatacreationtime, date) days
	, i.filename, i.isphoto, i.isvideo, i.mediametadatacreationtime, i.mediametadatavideostatus, i.producturl
from (
	select staticchatid, id staticmessageid,date, case when m.photo is not null then 'photo' else 'video' end type
	from videos.staticmessages m
	where 
		(m.photo is not null)
		or (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
) m
left join videos.uploadeditems u on u.staticchatid = m.staticchatid and u.staticmessageid = m.staticmessageid
left join videos.inventoryitems i on i.id = u.mediaitemid
where date < mediaitemmetadatacreationtime

-- test 7: no results
select m.*, u.status, u.statuscode, u.statusmessage, u.mediaitemid, u.mediaitemmetadatacreationtime, 
	datediff(minute, mediaitemmetadatacreationtime, date) minutes, datediff(day, mediaitemmetadatacreationtime, date) days
	, i.filename, i.isphoto, i.isvideo, i.mediametadatacreationtime, i.mediametadatavideostatus, i.producturl
from (
	select staticchatid, id staticmessageid,date, case when m.photo is not null then 'photo' else 'video' end type
	from videos.staticmessages m
	where 
		(m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
) m
left join videos.uploadeditems u on u.staticchatid = m.staticchatid and u.staticmessageid = m.staticmessageid
left join videos.inventoryitems i on i.id = u.mediaitemid
where isnull(mediametadatavideostatus, 'null') <> 'READY'

-- test 8: no results
-- just a logic test that the recursive cte is correct
;with m as (
	select staticchatid, id, replytomessageid, 1 as level
	from videos.staticmessages m
	where 
		(m.photo is not null)
		or (m.mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null'))
), r as (
	select staticchatid, id childid, replytomessageid parentid, 1 as level, 0 as isfinal
	from m
	where replytomessageid is not null
	union all
	select p.staticchatid, r.childid, isnull(p.replytomessageid, p.id) parentid, level + 1 as level, case when p.replytomessageid is null then 1 else 0 end isfinal
	from videos.staticmessages p
	inner join r on r.parentid = p.id and r.staticchatid = p.staticchatid
	where r.isfinal = 0
), roots as (
	select *
	from r
	where isfinal = 1
)

select 1
from r
where isfinal = 1
group by staticchatid, childid
having count(1) > 1
union all
select 1
from m
left join roots r on r.childid = m.id and r.staticchatid = m.staticchatid
left join videos.staticmessages rm on rm.staticchatid = m.staticchatid and rm.id = r.parentid
where m.replytomessageid is not null and r.staticchatid is null
	or rm.replytomessageid is not null
	or rm.action <> 'topic_created'

-- test 9: no results
select *
from videos.UploadedItems u
full join (

select * from videos.staticmessages where statictopicid is not null

) m on m.staticchatid = u.staticchatid and m.id = u.staticmessageid
where u.id is null or m.id is null
